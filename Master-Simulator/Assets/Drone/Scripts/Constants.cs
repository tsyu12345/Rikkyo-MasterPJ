namespace Constants {
    public static class Tags {
        public const string Tower = "Tower";
        public const string Agent = "Agent";
        public const string Evacuee = "Evacuee";
        public const string Obstacle = "Obstacle";
    }

    public enum DroneCtrlIndex {
        Horizontal = 2,
        Vertical = 1,
        Rotation = 0,
        Altitude = 0,
        Speed = 3
    }
}
