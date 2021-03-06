﻿using System;
using Titan.Bans;
using Titan.Util;
using Xunit;

namespace TitanTest
{
    public class BanManagerTest
    {

        // README: For this to work, you need to define the environment variable "TITAN_WEB_API_KEY"
        // and put into it your Steam Web API key. Failing to do so will skip these tests.

        private BanManager _banManager = new BanManager();

        public BanManagerTest()
        {
            _banManager.APIKey = Environment.GetEnvironmentVariable("TITAN_WEB_API_KEY");
        }

        [Fact]
        public void TestGameBan()
        {
            if(_banManager.APIKey != null)
            {
                var banInfo = _banManager.GetBanInfoFor(SteamUtil.FromSteamID("STEAM_0:0:208017504"));

                if(banInfo != null && banInfo.GameBanCount > 0)
                {
                    Assert.True(true, "topkektux has game bans on record.");
                }
                else
                {
                    Assert.True(false);
                }
                // STEAM_0:0:208017504
                // https://steamcommunity.com/id/TopKekTux/
            }
        }

        [Fact]
        public void TestVacBan()
        {
            if(_banManager.APIKey != null)
            {

                var banInfo = _banManager.GetBanInfoFor(SteamUtil.FromSteamID("STEAM_0:0:19877565"));

                if(banInfo != null && banInfo.VacBanned)
                {
                    Assert.True(true, "KQLY has VAC bans on record.");
                }
                else
                {
                    Assert.True(false);
                }
                // STEAM_0:0:19877565
                // https://steamcommunity.com/id/kqly/
            }
        }

        [Fact]
        public void TestCleanHistory()
        {
            if(_banManager.APIKey != null)
            {
                var banInfo = _banManager.GetBanInfoFor(SteamUtil.FromSteamID("STEAM_0:0:131983088"));

                if(banInfo != null && (!banInfo.VacBanned && banInfo.GameBanCount <= 0))
                {
                    Assert.True(true, "Marc3842h has no bans on record.");
                }
                else
                {
                    Assert.True(false);
                }
                // STEAM_0:0:131983088
                // https://steamcommunity.com/id/Marc3842h/
            }
        }

    }
}