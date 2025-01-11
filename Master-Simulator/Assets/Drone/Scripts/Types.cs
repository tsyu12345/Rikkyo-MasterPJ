using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Types : MonoBehaviour {
    
    [System.Serializable]
    public class MessageData {
        public string type;
        public string content;
    }
}

public interface IDroneAgent {
    List<GameObject> currentGuidedEvacuees { get; set; }
    int guidedCount { get; set; }
    void onAddEvacuee();
}
