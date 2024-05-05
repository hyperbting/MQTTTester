using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using UnityEngine;

public class MQTTConnect : MonoBehaviour
{
    public Action<string, string> OnTopicResponse;
    
    // Start is called before the first frame update
    void Start()
    {
    }

    public async Task Clean_Disconnect()
    {
        /*
         * This sample disconnects in a clean way. This will send a MQTT DISCONNECT packet
         * to the server and close the connection afterwards.
         *
         * See sample _Connect_Client_ for more details.
         */

        var mqttFactory = new MqttFactory();

        using (var mqttClient = mqttFactory.CreateMqttClient())
        {
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com").Build();
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            // This will send the DISCONNECT packet. Calling _Dispose_ without DisconnectAsync the 
            // connection is closed in a "not clean" way. See MQTT specification for more details.
            await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
        }
    }

    public async Task Connect_Client()
    {
        /*
         * This sample creates a simple MQTT client and connects to a public broker.
         *
         * Always dispose the client when it is no longer used.
         * The default version of MQTT is 3.1.1.
         */

        var mqttFactory = new MqttFactory();

        using (var mqttClient = mqttFactory.CreateMqttClient())
        {

            // Setup message handling before connecting so that queued messages
            // are also handled properly. When there is no event handler attached all
            // received messages get lost.
            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var msg = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array);
                
                Debug.LogFormat("Received application message: {0} {1}", e.ApplicationMessage.Topic,msg);
                OnTopicResponse?.Invoke(e.ApplicationMessage.Topic, msg);
                
                //e.DumpToConsole();

                return Task.CompletedTask;
            };
            
            // Use builder classes where possible in this project.
            var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("broker.hivemq.com").Build();

            // This will throw an exception if the server is not available.
            // The result from this message returns additional data which was sent 
            // from the server. Please refer to the MQTT protocol specification for details.
            var connectResponse = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            Debug.Log("The MQTT client is connected.");

            //response.DumpToConsole();

            var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(
                    f =>
                    {
                        f.WithTopic("mqttnet/samples/topic/2");
                    })
                .Build();

            var subResult = await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

            Debug.Log("MQTT client subscribed to topic.");

            await Task.Delay(60000);
            
            Debug.Log("Disconnecting");
            
            // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
            // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
            var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();

            await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);
        }
    }
}
