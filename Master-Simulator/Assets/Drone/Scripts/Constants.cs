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
        // ContinuousActions
        Speed = 0,
        /** 探索行動時における、座標出力用 */
        PosX  = 1,
        PosZ = 2,
        // DiscreteActions
        FlyMode = 0,
        Destination = 1,
    }

    public enum DroneCtrlIndex {
        Horizontal = 2,
        Vertical = 1,
        Rotation = 0,
        Altitude = 0,
        Speed = 3
    }
}
