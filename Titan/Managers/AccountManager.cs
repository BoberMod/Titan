﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Forms;
using Newtonsoft.Json;
using Serilog.Core;
using SteamKit2;
using Titan.Account;
using Titan.Account.Impl;
using Titan.Json;
using Titan.Logging;
using Titan.Mode;
using Titan.Util;

namespace Titan.Managers
{
    public class AccountManager
    {

        private Logger _log = LogCreator.Create();

        private Dictionary<int, List<TitanAccount>> _accounts = new Dictionary<int, List<TitanAccount>>();
        private List<TitanAccount> _allAccounts = new List<TitanAccount>();

        private Dictionary<int, long> _indexEntries = new Dictionary<int, long>();
        private int _index;

        private FileInfo _indexFile;
        private FileInfo _file;

        private JsonAccounts _parsedAccounts;
        private JsonIndex _parsedIndex;

        public AccountManager(FileInfo file)
        {
            _file = file;
            _indexFile = new FileInfo(Path.Combine(Environment.CurrentDirectory, "index.json"));
            _index = 0;

            var dirInfo = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "sentries"));
            if(!dirInfo.Exists)
            {
                dirInfo.Create();
            }

            _log.Debug("Titan Account Manager initialized on {TimeString}. ({UnixTimestamp})",
                DateTime.Now.ToShortTimeString(), Math.Round((double) TimeUtil.GetCurrentTicks()));
        }

        public bool ParseAccountFile()
        {
            if(!_file.Exists)
            {
                _log.Error("The accounts file at {0} doesn't exist! It is required and needs to " +
                           "have atleast one account specified.", _file.ToString());
                MessageBox.Show("The account file at " + _file + " doesn't exist. \nPlease create " +
                                "it and specify atleast one account.", "<!> Titan <!>", MessageBoxType.Error);
                return false;
            }

            using(var reader = File.OpenText(_file.ToString()))
            {
                _parsedAccounts = (JsonAccounts) new JsonSerializer().Deserialize(reader, typeof(JsonAccounts));
            }

            foreach(var indexes in _parsedAccounts.Indexes)
            {
                var accounts = new List<TitanAccount>();

                foreach(var account in indexes.Accounts)
                {
                    TitanAccount acc;
                    if(account.Sentry || account.SharedSecret != null)
                    {
                        acc = new ProtectedAccount(account);
                    }
                    else
                    {
                        acc = new UnprotectedAccount(account);
                    }

                    if(account.Enabled)
                    {
                        accounts.Add(acc);
                        _allAccounts.Add(acc);
                    }

                    _log.Debug("Found account (specified in index #{Index}): Username: {Username} / " +
                               "Password: {Password} / Sentry: {sentry} / Enabled: {Enabled}",
                        _index, account.Username, account.Password, account.Sentry, account.Enabled);
                }

                if(accounts.Count > 11)
                {
                    _log.Warning("You have more than 11 account specified in index {Index}. " +
                                 "It is recommend to specify max. 11 accounts.", _index);
                }

                _accounts.Add(_index, accounts);

                _index++;
            }

            if(_allAccounts.Count < 11)
            {
                _log.Warning("Less than 11 (only {Count}) accounts have been parsed. Atleast 11 accounts " +
                             "are required to get a target into Overwatch.", _allAccounts.Count);
                MessageBox.Show("You have less than 11 accounts specified. There are atleast 11 " +
                                "reports needed to get a target into Overwatch.", MessageBoxType.Warning);
            }
            else
            {
                var list = new List<object>();

                foreach(var keyPair in _accounts)
                {
                    list.Add(new { Index = keyPair.Key, keyPair.Value.Count });
                }

                _log.Information("Account file has been successfully parsed. {Count} accounts " +
                                 "have been parsed. Details: {@List}", _allAccounts.Count, list);
            }

            _index = 0;

            ParseIndexFile();

            return true;
        }

        private void ParseIndexFile()
        {
            if(_indexFile.Exists) // check if file exists
            {
                using(var reader = File.OpenText(_indexFile.ToString()))
                {
                    _parsedIndex = (JsonIndex) new JsonSerializer().Deserialize(reader, typeof(JsonIndex));
                    // read it and deserialize it into a Index object
                }

                var lowest = _parsedIndex.AvailableIndex; // find the available index and set it to lowest

                foreach(var expireEntry in _parsedIndex.Entries)
                {
                    // check if a entry that is marked for expiration is already expired and ready to bot
                    if(expireEntry.ExpireTimestamp <= TimeUtil.GetCurrentTicks())
                    {
                        _log.Debug("Index {Index} has expired. It is now available for botting.",
                            expireEntry.TargetedIndex);

                        if(lowest > expireEntry.TargetedIndex) // if thats the case, check if its lower than the available one
                        {
                            lowest = expireEntry.TargetedIndex;
                        }

                        _parsedIndex.Entries = _parsedIndex.Entries.Where(val => val != expireEntry)
                            .ToArray(); // and remove it from the expiration list
                    }
                    else
                    {
                        if(!_indexEntries.ContainsKey(expireEntry.TargetedIndex))
                        {
                            _indexEntries.Add(expireEntry.TargetedIndex, expireEntry.ExpireTimestamp);
                        }

                        _log.Debug("Index #{Index} hasn't expired yet. It will expire on {Time}.",
                            expireEntry.TargetedIndex,
                            TimeUtil.TicksToDateTime(expireEntry.ExpireTimestamp).ToShortTimeString());

                        if(expireEntry.TargetedIndex == lowest)
                        {
                            lowest++;
                        }
                    }
                }

                _index = lowest;
            }
            else
            {
                _index = 0;
                SaveIndexFile(); // it doesn't exist, we're gonna' create it!
            }

            var valid = false;
            foreach(var keyVal in _accounts)
            {
                if(keyVal.Key == _index)
                {
                    _log.Debug("Using index #{Index} for botting.", _index);
                    valid = true;
                }
            }

            if(!valid)
            {
                _log.Warning("Index #{index} doesn't exist. The Bot will use index " +
                             "#{ForcedIndex}. Please keep in mind that it may already " +
                             "been used.", _index, _index = 0);
            }

        }

        public void SaveIndexFile()
        {
            if(_indexFile.Exists)
            {
                _indexFile.Delete();
            }

            var jsonIndex = new JsonIndex
            {
                AvailableIndex = _index,
                Entries = _indexEntries.Select(keyVal => new JsonIndex.JsonEntry
                    {
                        TargetedIndex = keyVal.Key,
                        ExpireTimestamp = keyVal.Value
                    })
                    .ToArray()
            };

            using(var writer = File.CreateText(_indexFile.ToString()))
            {
                var str = JsonConvert.SerializeObject(jsonIndex, Formatting.Indented);
                writer.Write(str);
            }

            _log.Debug("Successfully wrote index file.");
        }

        public void StartBotting(BotMode mode, SteamID target, ulong matchId)
        {
            _log.Debug("Checking if the index file contains a newer available index...");

            ParseIndexFile(); // Before we bot, we parse the index file to get the latest available index

            _log.Debug("Starting botting using index {Index}.", _index);

            List<TitanAccount> accounts;
            if(_accounts.TryGetValue(_index, out accounts))
            {
                try
                {
                    Titan.Instance.ThreadManager.StartWatchdog();
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Could not start Watchdog thread, starting to report without one!");
                }

                foreach(var acc in accounts)
                {
                    try
                    {
                        Titan.Instance.ThreadManager.Start(mode, acc, target.AccountID, matchId);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Could not start reporting for account {Account}: {Message}",
                            acc.JsonAccount.Username, ex.Message);
                    }
                }
            }
            else
            {
                _log.Error("Could not export accounts for current index {Index}. " +
                           "Does it exist?", _index);
                return;
            }
            
            if(mode == BotMode.Report)
                Titan.Instance.VictimTracker.AddVictim(target);

            if(!_indexEntries.ContainsKey(_index))
            {
                _indexEntries.Add(_index, TimeUtil.DateTimeToTicks(DateTime.Now.AddHours(6)));
                SaveIndexFile();
            }

            _log.Debug("Index #{Index} has been used. It will be unlocked on {Unlock}. " +
                       "Using index #{NextIndex} for next botting session.",
                _index, DateTime.Now.AddHours(6).ToLongTimeString(), ++_index);
        }

    }
}