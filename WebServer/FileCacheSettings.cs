namespace WebServer
{
    internal struct FileCacheSettings
    {
        public int cacheClearingInterval;
        public int initLife;
        public int firstGenLifeMax;
        public int firstGenLifeGrowth;
        public int secondGenLifeMax;
        public int secondGenLifeGrowth;
        public int thirdGenLifeMax;
        public int thirdGenLifeGrowth;
    }
}
