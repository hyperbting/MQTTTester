using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MQTTConnectUser : MonoBehaviour
{
    [SerializeField]private Text _text;
    private MQTTConnect con;

    private void OnEnable()
    {
        con = gameObject.GetComponent<MQTTConnect>();
        con.OnTopicResponse += DisplayMQTTMessage;
    }

    private void OnDisable()
    {
        con.OnTopicResponse -= DisplayMQTTMessage;
    }

    // Start is called before the first frame update
    void Start()
    {
        _ = con.Connect_Client();
    }

    private void Update()
    {
        _text.text = newContent;
    }

    string newContent = ""; 
    private void DisplayMQTTMessage(string topic, string msg)
    { 
        newContent = DateTime.Now.ToShortTimeString() + "@"+topic + ":" + msg + "\n" + newContent; 
        // _text.text = newContent;
    }
}
