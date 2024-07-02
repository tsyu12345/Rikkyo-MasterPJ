namespace Constants {
    public static class Tags {
        public const string Tower = "Tower";
        public const string Agent = "Agent";
        public const string Evacuee = "Evacuee";
        public const string Obstacle = "Obstacle";
    }

    public enum ControlModel {
        Physics = 1,
        NavMesh = 2,

    }

    /// <summary>
    /// ナビメッシュ版エージェントにおける、各種行動の格納先Index
    /// </summary>
    public enum NavAgentCtrlIndex {
        Speed = 0,
        Altitude = 1,
        Destination = 2,
        /** 探索行動時における、座標出力用 */
        PosX  = 3,
        PosZ = 4,
    }

    public enum DroneCtrlIndex {
        Horizontal = 2,
        Vertical = 1,
        Rotation = 0,
        Altitude = 0,
        Speed = 3
    }
}
