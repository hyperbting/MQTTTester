using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using UnityEngine;

public class MQTTConnect : MonoBehaviour
{
    #region Broker
    private Uri _mqttBroker = new Uri("mqtts://broker.hivemq.com:1883");
    public Uri SetMqttBroker
    {
        set => _mqttBroker = value;
    }
    #endregion
    
    #region Topic 
    private List<string> _mqttTopics = new List<string>(){"testtopic/#"};
    public List<string> MqttBrokerTopics
    {
        set => _mqttTopics = value;
        //get => _mqttBroker;
    }
    #endregion
    
    private int _mqttPort = 1883;
    private CancellationTokenSource cts;
    
    public Action OnBrokerConnecting;
    public Action<MqttClientConnectResult> OnBrokerConnected;
    public Action OnBrokerDisconnected;
    public Action<MqttClientSubscribeResult> OnTopicSubscribed;
    public Action<string, string> OnTopicResponse;

    public void OnDisable()
    {
        TryDisconnect();
    }

    public void TryDisconnect() => cts?.Cancel();

    public async Task ConnectBroker(string user, string passwd, string clientID)
    {
        cts = new CancellationTokenSource();

        var mqttFactory = new MqttFactory();

        using (var mqttClient = mqttFactory.CreateMqttClient())
        {
            ConfigureMqttClientEvents(mqttClient);

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithCredentials(user, passwd) // Set username and password
                .WithClientId(clientID)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                //.WithTcpServer(_mqttBroker, _mqttPort)
                .WithConnectionUri(_mqttBroker) // mqttnet DOESN'T support multiple URI at the same time
                .Build();

            try
            {
                // This will throw an exception if the server is not available.
                // The result from this message returns additional data which was sent 
                // from the server. Please refer to the MQTT protocol specification for details.
                var conResp = await mqttClient.ConnectAsync(mqttClientOptions, cts.Token);
                _ = HandleConnectResponse(conResp);
            }
            catch (Exception ex)
            {
                // Handle the exception, log it, or take appropriate action.
                Debug.LogError($"Error connecting to MQTT broker: {ex.Message}");
                return;
            }
            
            _ = await SubscribeToTopics(mqttClient, mqttFactory);
            
            await MaintainConnection(mqttClient);
            await UnsubscribeAndDisconnect(mqttClient, mqttFactory);
        }
    }

    #region ConnectBroker SubFunctions
    private async Task UnsubscribeAndDisconnect(IMqttClient mqttClient, MqttFactory mqttFactory)
    {

        // want Unsubscribe at most 5sec(=5000ms)
        var ctsUnsubscribing = new CancellationTokenSource(5000);
        
        Debug.Log("MQTTConnecter Unsubscribing");
        var mqttUnsubscribeOptions = mqttFactory.CreateUnsubscribeOptionsBuilder();
        foreach (var tp in _mqttTopics)
        {
            mqttUnsubscribeOptions.WithTopicFilter(tp);
        }
            
        try
        {
            var subResult = await mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions.Build(), ctsUnsubscribing.Token);
            //OnTopicUnsubscribed?.Invoke(subResult);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error Unsubscribe to MQTT broker: {ex.Message}");
            return;
        }
        
        Debug.Log("MQTTConnecter Disconnecting");
        // This will send the DISCONNECT packet. Calling _Dispose_ without DisconnectAsync the connection is closed in a "not clean" way. See MQTT specification for more details.
        await mqttClient.DisconnectAsync(
            new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build(), 
            CancellationToken.None
            );
    }

    private async Task MaintainConnection(IMqttClient mqttClient)
    {
        while (!cts.IsCancellationRequested)
        {
            if (mqttClient.IsConnected)
            {
                await Task.Delay(1000, cts.Token);
                continue;
            }
        
            // NOT connected
            Debug.LogWarning("MQTTConnecter MaintainConnection: MQTT client Not Connected.");
            cts.Cancel();
        }
    }

    private async Task<bool> SubscribeToTopics(IMqttClient mqttClient, MqttFactory mqttFactory)
    {
        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(
                f =>
                {
                    foreach (var tp in _mqttTopics)
                    {
                        f.WithTopic(tp);
                    }
                })
            .Build();
        
        try
        {
            var subResult = await mqttClient.SubscribeAsync(mqttSubscribeOptions, cts.Token);
            OnTopicSubscribed?.Invoke(subResult);

            Debug.Log("MQTT client subscribed to topic.");
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error Subscribe to MQTT broker: {ex.Message}");
        }

        return false;
    }

    private async Task<bool> PublishToTopics(IMqttClient mqttClient, MqttFactory mqttFactory, string topic, string content, bool retained=false)
    {
        var mqttOpt = mqttFactory.CreateApplicationMessageBuilder();
        mqttOpt.WithTopic(topic)
            .WithPayload(content)
            //.WithAtLeastOnceQoS()
            //.WithDupFlag(false)
            .WithRetainFlag(retained);

        try
        {
            var subResult = await mqttClient.PublishAsync(mqttOpt.Build(), CancellationToken.None);
            //OnTopicPublished?.Invoke(subResult);

            if (subResult.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                Debug.Log("MQTT client published to Broker.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error Publishing to MQTT broker: {ex.Message}");
        }

        Debug.LogError($"Failed to Publish to MQTT broker");
        return false;
    }
    
    private bool HandleConnectResponse(MqttClientConnectResult conResp)
    {
        switch (conResp.ResultCode)
        {
            case MqttClientConnectResultCode.Success:
                OnBrokerConnected?.Invoke(conResp);
                return true;//break;
            default:
                Debug.LogError("MQTTConnecter The MQTT connect response:" + conResp.ResultCode);
                break;
        }

        return false;
    }

    private void ConfigureMqttClientEvents(IMqttClient mqttClient)
    {
        // Setup message handling before connecting so that queued messages
        // are also handled properly. When there is no event handler attached all
        // received messages get lost.
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var msg = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array);
            
            Debug.LogFormat("MQTTConnecter Received application message: {0} {1}", e.ApplicationMessage.Topic,msg);
            OnTopicResponse?.Invoke(e.ApplicationMessage.Topic, msg);

            return Task.CompletedTask;
        };

        mqttClient.ConnectingAsync += async e =>
        {
            Debug.Log("MQTTConnecter Connecting to MQTT broker..." + e.ToString());
            OnBrokerConnecting?.Invoke();
            
            await Task.CompletedTask;
        };

        mqttClient.ConnectedAsync += async e =>
        {
            Debug.Log("MQTTConnecter Successfully connected to MQTT broker." + e.ToString());
            
            await Task.CompletedTask;
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            Debug.LogWarning("MQTTConnecter MQTT client disconnected." + e.ToString());
            OnBrokerDisconnected?.Invoke();
            
            await Task.CompletedTask;// await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
        };
    }
    #endregion
}