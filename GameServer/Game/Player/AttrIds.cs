namespace MikuSB.GameServer.Game.Player;

public static class AttrIds
{
    public const uint CurrencyGid = 1;

    public static class BossPvp
    {
        public const uint Gid = 51;
        public const uint ActivitySid = 0;
        public const uint ChallengeNumSid = 1;
        public const uint LevelStartSid = 100;
    }

    public static class Quest
    {
        public const uint LevelStateGid = 21;
        public const uint LevelPassGid = 22;
        public const uint SettlementSeedGid = 23;
        public const uint ChapterStarAwardGid = 20;
        public const uint ChapterStarAwardMaskVersionSid = 0;
    }

    public static class BattlePass
    {
        public const uint Gid = 25;
        public const uint CurrentIdSid = 1;
        public const uint StatusSid = 2;
    }

    public static class Dlc
    {
        public const uint Gid = 15;
        public const uint ActIdSid = 1;
    }

    public static class Fishing
    {
        public const uint Gid = 32;
        public const uint FoodBaseSid = 30000;
    }

    public static class DreamCard
    {
        public const uint DataGid = 62;
        public const uint LevelGid = 152;
    }

    public static class Adjust
    {
        public const uint Gid = 107;
    }

    public static class Gacha
    {
        public const uint Gid = 5;
        public const uint StringGid = 42;
        public const uint TotalTimeSid = 1;
        public const uint DailyTotalTimeSid = 2;
        public const uint TimeInheritStartSid = 20000;
        public const uint TimeNotInheritStartSid = 10;
        public const uint AddTimeItemSid = 1;
        public const uint AddTimeProbSid = 2;
        public const uint AddProtectTypeSid = 3;
        public const uint AddTotalTimeSid = 7;
    }

    public static class Girl
    {
        public const uint SpineStringGid = 30;
    }

    public static class Scene
    {
        public const uint MainGid = 132;
        public const uint MainSid = 1;
    }

    public static class Shop
    {
        public const uint PurchaseGid = 26;
        public const uint RedDotGid = 113;
    }

    public static class Settings
    {
        public const uint Gid = 44;
    }

    public static class Rogue3D
    {
        public const uint Gid = 124;
        public const uint CurDiffSid = 5;
        public const uint GameplayIdSid = 6;
        public const uint TalentIdSid = 7;
        public const uint SeasonGameplayIdSid = 1006;
        public const uint SeasonTalentIdSid = 1007;
        public const uint SeasonEnterFlagSid = 1008;
        public const uint LevelPassStartSid = 20;
        public const uint DailyBuffStartSid = 51;
        public const uint DailyBuffEndSid = 65;
    }

    public static class SupporterCard
    {
        public const uint Gid = 150;
        public const uint FixedResetSid = 1;
    }

    public static class Tower
    {
        public const uint Gid = 3;
        public const uint LevelStateGid = 21;
        public const uint PassGid = 22;
        public const uint BasicProgressSid = 2;
        public const uint AdvancedProgressSid = 3;
        public const uint TimeSid = 1;
        public const uint DiffSid = 4;
        public const uint HistoryDiffSid = 5;
        public const uint RewardStateSidBase = 100;
        public const uint LevelStateSidBase = 10000;
    }

    public static class House
    {
        public const uint Gid = 101;
        public const uint BedroomStartSid = 2550;
        public const uint PlayerRingInfoSidBase = 3174;
    }

    public static class VirCapture
    {
        public const uint Gid = 128;
        public const uint RikiGid = 135;
        public const uint FormationStringGid = 57;
        public const uint ActivitySid = 1;
        public const uint FormationSid = 1;
        public const uint CurrentExpSid = 2;
        public const uint CurrentLevelSid = 3;
        public const uint BagNumSid = 5;
        public const uint TrialActIdSid = 6;
        public const uint DailyExpSid = 8;
        public const uint SeasonActIdSid = 9;
        public const uint ColorMaxStartSid = 11;
        public const uint LevelAwardFlagStartSid = 101;
        public const uint LevelAwardFlagEndSid = 120;
        public const uint MapDataStartSid = 10000;
        public const uint MaxMapDataLength = 3000;
    }
}
