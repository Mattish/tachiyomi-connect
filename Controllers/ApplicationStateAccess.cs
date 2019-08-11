using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TachiyomiConnect.Dtos;

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
                account = dbState.Accounts.FirstOrDefault(x => x.Devices.Contains(accountDevice));
                return account != null;
            }
        }

        public static void RegisterNewAccount(AccountDevice device, Account account, Guid recoveryCode)
        {
            lock (dbLock)
            {
                var dbState = GetDbState();
                dbState.Accounts.Add(account);
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

        public static bool TryAddAccountDevice(AccountDevice accountDevice, string accountCode)
        {
            timedAccountCodes = timedAccountCodes.Where(x => x.ValidUntil > DateTimeOffset.UtcNow).ToList();

            var foundTimedAccountCode = timedAccountCodes.FirstOrDefault(x => x.Code == accountCode);

            if (foundTimedAccountCode != null)
            {
                lock (dbLock)
                {
                    var dbState = GetDbState();
                    var existingAccount = dbState.Accounts.FirstOrDefault(x => x.Id == foundTimedAccountCode.AccountId);
                    if (existingAccount != null)
                    {
                        existingAccount.Devices.Add(accountDevice);
                        WriteDbState(dbState);
                        return true;
                    }
                }
            }

            return false;

        }

        public static void AddTimedAccountCode(TimedAccountCode timedAccountCode)
        {
            timedAccountCodes.Add(timedAccountCode);
        }

        public static void AddSyncStateToAccount(Account account, StateResponseDto dto)
        {
            lock (dbLock)
            {
                var dbState = GetDbState();
                var existingAccount = dbState.Accounts.First(x => x.Id == account.Id);
                existingAccount.SyncStates.Add(dto);
                WriteDbState(dbState);
            }
        }

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto
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