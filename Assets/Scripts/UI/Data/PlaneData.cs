namespace LeiTing.UI
{
    public enum PlaneUnlockType
    {
        Default = 0,
        Coin = 1,
        Diamond = 2,
        Ad = 3
    }

    public class PlaneData
    {
        public int id;
        public string name;
        public string iconPath;
        public string prefabPath;

        public int hp;
        public int attack;
        public float fireRate;
        public float moveSpeed;

        public bool owned;
        public bool selected;

        public PlaneUnlockType unlockType;
        public int adCountRequired;
        public int adCountWatched;
    }
}
