using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MQTTConnectUser : MonoBehaviour
{

    private MQTTConnect con;
    // Start is called before the first frame update
    void Start()
    {
        con = gameObject.GetComponent<MQTTConnect>();
        _ = con.Connect_Client();
    }
}
