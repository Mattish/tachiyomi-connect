using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation.PredefinedTransformations;

namespace TachiyomiConnect.Controllers
{
    public static class ApplicationStateAccess
    {
        private static readonly string DbFileName = "TachiyomiConnect.json";
        private static readonly object dbLock = new object();

        private static List<TimedAccountCode> timedAccountCodes = new List<TimedAccountCode>();

        public static bool TryGetAccount(AccountDevice accountDevice, out Account account)
        {
            lock (dbLock)
            {
                var dbState = GetDbState();
                if (dbState.AccountDeviceToAccount.TryGetValue(accountDevice, out account))
                {
                    WriteDbState(dbState);
                    return true;
                }

                return false;
            }
        }

        public static void RegisterNewAccount(AccountDevice device, Account account, Guid recoveryCode)
        {
            lock (dbLock)
            {
                var dbState = GetDbState();
                dbState.AccountDeviceToAccount[device] = account;
                dbState.RecoveryCodeToAccountDevice[recoveryCode] = device;
                WriteDbState(dbState);
            }
        }

        public static bool TryGetAccountDevice(Guid recoveryCode, out AccountDevice accountDevice)
        {
            lock (dbLock)
            {
                var dbState = GetDbState();
                return dbState.RecoveryCodeToAccountDevice.TryGetValue(recoveryCode, out accountDevice);
            }
        }

        public static bool TryAddAccountDevice(AccountDevice accountDevice, string accountCode, out TimedAccountCode timedAccountCode)
        {
            timedAccountCodes = timedAccountCodes.Where(x => x.ValidUntil > DateTimeOffset.UtcNow).ToList();

            timedAccountCode = timedAccountCodes.FirstOrDefault(x => x.Code == accountCode);

            if (timedAccountCode != null)
            {
                lock (dbLock)
                {
                    var dbState = GetDbState();
                    dbState.AccountDeviceToAccount[accountDevice] = timedAccountCode.Account;
                    timedAccountCode.Account.Devices.Add(accountDevice);
                    WriteDbState(dbState);
                    return true;
                }
            }

            return false;

        }

        public static void AddTimedAccountCode(TimedAccountCode timedAccountCode)
        {

        }

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Objects
        };

        private static TachiyomiDbState GetDbState()
        {
            if (!File.Exists(DbFileName))
            {
                WriteDbState(new TachiyomiDbState());
            }
            var text = File.ReadAllText(DbFileName);
            return JsonConvert.DeserializeObject<TachiyomiDbState>(text, jsonSettings);
        }

        private static void WriteDbState(TachiyomiDbState state)
        {
            var json = JsonConvert.SerializeObject(state, jsonSettings);
            File.WriteAllText(DbFileName, json);
        }

    }
}