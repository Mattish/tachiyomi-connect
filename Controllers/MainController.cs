using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TachiyomiConnect.Dtos;

namespace TachiyomiConnect.Controllers
{
    public class AccountDevice
    {
        public Guid DeviceId { get; }

        public string SecretToken { get; }

        public Guid RecoveryCode { get; }

        public AccountDevice(Guid deviceId, string secretToken, Guid recoveryCode)
        {
            DeviceId = deviceId;
            SecretToken = secretToken;
            RecoveryCode = recoveryCode;
        }

        protected bool Equals(AccountDevice other)
        {
            return DeviceId.Equals(other.DeviceId) && string.Equals(SecretToken, other.SecretToken);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AccountDevice)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DeviceId.GetHashCode() * 397) ^ (SecretToken != null ? SecretToken.GetHashCode() : 0);
            }
        }
    }
    public class Account
    {
        public readonly List<AccountDevice> Devices;
        public List<StateResponseDto> SyncStates;

        public Account(List<AccountDevice> devices, StateResponseDto initialState)
        {
            Devices = devices;
            SyncStates = new List<StateResponseDto>(new[] { initialState });
        }

    }

    public class TimedAccountCode
    {
        public string Code { get; }
        public DateTimeOffset ValidUntil { get; }
        public Account Account { get; }
        public TimedAccountCode(string code, DateTimeOffset validUntil, Account account)
        {
            Code = code;
            ValidUntil = validUntil;
            Account = account;
        }

        protected bool Equals(TimedAccountCode other)
        {
            return string.Equals(Code, other.Code);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TimedAccountCode)obj);
        }

        public override int GetHashCode()
        {
            return (Code != null ? Code.GetHashCode() : 0);
        }
    }

    public class TachiyomiDbState
    {
        public Dictionary<AccountDevice, Account> AccountDeviceToAccount = new Dictionary<AccountDevice, Account>();
        public Dictionary<Guid, AccountDevice> RecoveryCodeToAccountDevice = new Dictionary<Guid, AccountDevice>();
    }

    [Route("")]
    [ApiController]
    public class MainController : ControllerBase
    {

        private static readonly RandomNumberGenerator Rng = new RNGCryptoServiceProvider();

        [HttpGet("")]
        public ActionResult Get404()
        {
            return NotFound();
        }

        [HttpGet("version")]
        public ActionResult Get([FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Authorization")] string token)
        {
            var accountDevice = new AccountDevice(deviceId, token, Guid.Empty);
            if (!ApplicationStateAccess.TryGetAccount(accountDevice, out var account))
            {
                return new UnauthorizedResult();
            }

            var lastState = account.SyncStates.Last();
            return new JsonResult(new VersionResponseDto
            {
                Guid = lastState.Guid,
                VersionNumber = lastState.VersionNumber
            });
        }

        [HttpGet("states")]
        public ActionResult GetStates([FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Authorization")] string token, [FromQuery(Name = "fromVersion")] int fromVersion)
        {
            var accountDevice = new AccountDevice(deviceId, token, Guid.Empty);
            if (!ApplicationStateAccess.TryGetAccount(accountDevice, out var account))
            {
                return new UnauthorizedResult();
            }

            if (fromVersion == 0)
            {
                var remainingStates = account.SyncStates.ToArray();
                var response = new StatesResponseDto
                {
                    FromVersionNumber = fromVersion,
                    States = remainingStates
                };
                return new JsonResult(response);
            }
            else
            {
                int fromIndex = account.SyncStates.FindIndex(0, existingDto => existingDto.VersionNumber == fromVersion);

                if (fromIndex > -1)
                {
                    var remainingStates = account.SyncStates.Skip(fromIndex + 1).ToArray();
                    var response = new StatesResponseDto
                    {
                        FromVersionNumber = fromVersion,
                        States = remainingStates
                    };
                    return new JsonResult(response);
                }

                return BadRequest($"Unable to find existing State for VersionId:{fromVersion}");
            }
        }

        [HttpPost("state")]
        public ActionResult PostState([FromBody] StateResponseDto stateResponseChange, [FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Authorization")] string token)
        {
            var newKey = new AccountDevice(deviceId, token, Guid.Empty);
            if (!ApplicationStateAccess.TryGetAccount(newKey, out var account))
            {
                return new UnauthorizedResult();
            }

            if (account.SyncStates.Last().VersionNumber + 1 != stateResponseChange.VersionNumber)
            {
                return BadRequest(
                    $"The Version Number of '{stateResponseChange.VersionNumber}' is not expected. Expected:{account.SyncStates.Last().VersionNumber + 1}");
            }

            account.SyncStates.Add(stateResponseChange);
            return new JsonResult(account.SyncStates.Last());
        }

        [HttpPost("register")]
        public ActionResult Post([FromBody] StateResponseDto stateResponseChange, [FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Recovery-Code")] Guid recoveryCode)
        {
            stateResponseChange.Guid = stateResponseChange.Guid;
            stateResponseChange.VersionNumber = 1;

            byte[] tokenData = new byte[256];
            Rng.GetBytes(tokenData);

            string secretToken = Convert.ToBase64String(tokenData);

            var newDevice = new AccountDevice(deviceId, secretToken, recoveryCode);
            var newAccount = new Account(new List<AccountDevice>(new[] { newDevice }), stateResponseChange);
            ApplicationStateAccess.RegisterNewAccount(newDevice, newAccount, recoveryCode);

            var registrationResponseDto = new RegistrationResponseDto
            {
                DeviceId = deviceId,
                RecoveryCode = recoveryCode,
                SecretToken = secretToken,
                InitialState = stateResponseChange
            };

            Console.WriteLine($"Register new device. DeviceId:{deviceId} RecoveryCode:{recoveryCode} SecretToken:{secretToken}");
            return new JsonResult(registrationResponseDto);
        }

        [HttpPost("register/recovery")]
        public ActionResult PostRecovery([FromHeader(Name = "Recovery-Code")] Guid recoveryCode)
        {
            byte[] tokenData = new byte[256];
            Rng.GetBytes(tokenData);

            string secretToken = Convert.ToBase64String(tokenData);

            if (ApplicationStateAccess.TryGetAccountDevice(recoveryCode, out var existingAccountDevice) && ApplicationStateAccess.TryGetAccount(existingAccountDevice, out var existingAccount))
            {
                var newAccountDevice = new AccountDevice(existingAccountDevice.DeviceId, secretToken, Guid.NewGuid());

                var registrationResponseDto = new RegistrationResponseDto
                {
                    DeviceId = newAccountDevice.DeviceId,
                    RecoveryCode = newAccountDevice.RecoveryCode,
                    SecretToken = newAccountDevice.SecretToken,
                    InitialState = existingAccount.SyncStates.First()
                };

                existingAccount.Devices.Add(newAccountDevice);
                existingAccount.Devices.Remove(existingAccountDevice);

                Console.WriteLine($"Recovered device from recovery code {recoveryCode}. New AccountDevice DeviceId:{newAccountDevice.DeviceId} RecoveryCode:{newAccountDevice.RecoveryCode} SecretToken:{newAccountDevice.SecretToken}");
                return new JsonResult(registrationResponseDto);
            }

            return BadRequest($"This recovery code does not match to any known account device");
        }

        [HttpPost("register/code")]
        public ActionResult PostCode([FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Recovery-Code")] Guid recoveryCode, [FromHeader(Name = "Account-Code")] string accountCode)
        {
            byte[] tokenData = new byte[256];
            Rng.GetBytes(tokenData);
            string secretToken = Convert.ToBase64String(tokenData);
            var accountDevice = new AccountDevice(deviceId, secretToken, recoveryCode);
            if (ApplicationStateAccess.TryAddAccountDevice(accountDevice, accountCode, out var timedAccountCode))
            {

                var registrationResponseDto = new RegistrationResponseDto
                {
                    DeviceId = accountDevice.DeviceId,
                    RecoveryCode = accountDevice.RecoveryCode,
                    SecretToken = accountDevice.SecretToken,
                    InitialState = timedAccountCode.Account.SyncStates.First()
                };
                return new JsonResult(registrationResponseDto);
            }

            return NotFound();
        }

        [HttpGet("code")]
        public ActionResult GetCode([FromHeader(Name = "Device-ID")] Guid deviceId, [FromHeader(Name = "Authorization")] string token)
        {
            var accountDevice = new AccountDevice(deviceId, token, Guid.Empty);
            if (!ApplicationStateAccess.TryGetAccount(accountDevice, out var account))
            {
                return new UnauthorizedResult();
            }

            byte[] tokenData = new byte[4];
            Rng.GetBytes(tokenData);
            var timedAccountCode = new TimedAccountCode(ByteArrayToHexString(tokenData), DateTimeOffset.UtcNow.AddMinutes(1), account);

            ApplicationStateAccess.AddTimedAccountCode(timedAccountCode);

            return new JsonResult(new AccountCodeResponseDto
            {
                Code = timedAccountCode.Code,
                ValidUntil = timedAccountCode.ValidUntil
            });
        }

        //https://stackoverflow.com/questions/623104/byte-to-hex-string
        public static string ByteArrayToHexString(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            const string hexAlphabet = "0123456789ABCDEF";

            foreach (byte b in bytes)
            {
                result.Append(hexAlphabet[b >> 4]);
                result.Append(hexAlphabet[b & 0xF]);
            }

            return result.ToString();
        }
    }
}
