/*
 * open-iec104-mqtt 
 * Copyright (C) 2026 Hephario GmbH
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

using lib60870;
using lib60870.CS101;
using lib60870.CS104;

namespace OpenIec104MqttBridge
{
    class Program
    {
        static IMqttClient mqttClient = null!;
        static string bridgeId = "";
        static MqttQualityOfServiceLevel qosLevel;
        static bool mqttRetain;
        static bool requireTransactionId;
        static ConcurrentDictionary<int, string> pendingTransactions = new();

        static Connection iecClientConnection = null!;
        static Server iecServer = null!;

        /// <summary>
        /// Entry point: Initializes configuration, MQTT client, and IEC 104 client/server.
        /// </summary>
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting open-iec104 MQTT Bridge (C#)...");

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
                
            IConfiguration config = builder.Build();

            bridgeId = config["BRIDGE_ID"] ?? config["BridgeId"] ?? "default_bridge";
            string mode = (config["IEC104_MODE"] ?? config["Mode"] ?? "client").ToLower();
            
            string mqttHost = config["MQTT_HOST"] ?? config["Mqtt:Host"] ?? "127.0.0.1";
            int mqttPort = int.Parse(config["MQTT_PORT"] ?? config["Mqtt:Port"] ?? "1883");
            int mqttQos = int.Parse(config["MQTT_QOS"] ?? "1");
            mqttRetain = bool.Parse(config["MQTT_RETAIN"] ?? "false");
            requireTransactionId = bool.Parse(config["REQUIRE_TRANSACTION_ID"] ?? "false");

            string iecIp = config["IEC104_IP"] ?? config["Iec104:Ip"] ?? "127.0.0.1";
            int iecPort = int.Parse(config["IEC104_PORT"] ?? config["Iec104:Port"] ?? "2404");

            qosLevel = mqttQos == 2 ? MqttQualityOfServiceLevel.ExactlyOnce : 
                       mqttQos == 1 ? MqttQualityOfServiceLevel.AtLeastOnce : 
                       MqttQualityOfServiceLevel.AtMostOnce;

            var mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
            
            var mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId($"iec104_bridge_{bridgeId}_{Guid.NewGuid()}")
                .WithTcpServer(mqttHost, mqttPort)
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithCleanSession()
                .Build();

            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payloadStr = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                Console.WriteLine($"[MQTT -> IEC104] Received: {topic} -> {payloadStr}");
                
                try 
                {
                    var parts = topic.Split('/');
                    if (mode == "client" && parts.Length >= 5 && parts[2] == "command")
                    {
                        await Task.Yield();
                        string type = parts[3];
                        int ioa = int.Parse(parts[4]);
                        var doc = JsonDocument.Parse(payloadStr);
                        
                        string transactionId = null;
                        if (doc.RootElement.TryGetProperty("transaction_id", out var tidElement))
                        {
                            transactionId = tidElement.GetString();
                        }
                        
                        if (requireTransactionId && string.IsNullOrEmpty(transactionId))
                        {
                            Console.WriteLine($"[Warning] Command rejected: transaction_id is required but missing for IOA {ioa}");
                            return;
                        }
                        
                        if (!string.IsNullOrEmpty(transactionId))
                        {
                            pendingTransactions[ioa] = transactionId;
                        }
                        
                        if (iecClientConnection != null) 
                        {
                            if (type == "cmd" || type == "sp") 
                            {
                                bool execute = doc.RootElement.GetProperty("execute").GetBoolean();
                                iecClientConnection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new SingleCommand(ioa, execute, false, 0));
                                Console.WriteLine($"Sent SingleCommand to IOA {ioa}: {execute}");
                            }
                            else if (type == "dp")
                            {
                                int val = doc.RootElement.GetProperty("value").GetInt32();
                                iecClientConnection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new DoubleCommand(ioa, val, false, 0));
                                Console.WriteLine($"Sent DoubleCommand to IOA {ioa}: {val}");
                            }
                            else if (type == "setpoint_float")
                            {
                                // IEC104 only supports 32-bit floats natively (Short Floating Point)
                                float val = (float)doc.RootElement.GetProperty("value").GetDouble();
                                iecClientConnection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new SetpointCommandShort(ioa, val, new SetpointCommandQualifier(0)));
                                Console.WriteLine($"Sent SetpointCommandShort to IOA {ioa}: {val}");
                            }
                            else if (type == "setpoint_scaled")
                            {
                                short val = doc.RootElement.GetProperty("value").GetInt16();
                                iecClientConnection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new SetpointCommandScaled(ioa, new ScaledValue(val), new SetpointCommandQualifier(0)));
                                Console.WriteLine($"Sent SetpointCommandScaled to IOA {ioa}: {val}");
                            }
                        }
                    }
                    else if (mode == "server" && parts.Length >= 5 && parts[2] == "telemetry_in")
                    {
                        // Server mode telemetry push is a stub for simulation.
                        Console.WriteLine($"Server mode telemetry push not fully implemented for dynamic mapping. Received: {topic}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing MQTT message: {ex.Message}");
                }
            };

            mqttClient.DisconnectedAsync += async e =>
            {
                Console.WriteLine($"[WARNING] MQTT Disconnected: {e.Reason}. Attempting reconnect in 5s...");
                await Task.Delay(5000);
                try {
                    await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
                    // Resubscribe because we use CleanSession=true
                    string topic = mode == "client" ? $"iec104/{bridgeId}/command/#" : $"iec104/{bridgeId}/telemetry_in/#";
                    await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(qosLevel).Build());
                    Console.WriteLine("MQTT Reconnected and resubscribed successfully.");
                } catch (Exception ex) {
                    Console.WriteLine($"MQTT Reconnect failed: {ex.Message}");
                }
            };

            Console.WriteLine($"Connecting to MQTT Broker at {mqttHost}:{mqttPort} (MQTTv5)");
            while (!mqttClient.IsConnected)
            {
                try {
                    await mqttClient.ConnectAsync(mqttOptions, CancellationToken.None);
                } catch {
                    Console.WriteLine("MQTT connection failed, retrying in 2 seconds...");
                    await Task.Delay(2000);
                }
            }
            
            string cmdTopic = mode == "client" ? $"iec104/{bridgeId}/command/#" : $"iec104/{bridgeId}/telemetry_in/#";

            await mqttClient.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic(cmdTopic).WithQualityOfServiceLevel(qosLevel).Build());
            Console.WriteLine($"Subscribed to {cmdTopic} (QoS: {mqttQos})");

            if (mode == "client")
            {
                if (!System.Net.IPAddress.TryParse(iecIp, out _))
                {
                    Console.WriteLine($"Resolving hostname {iecIp} to IPv4...");
                    var addresses = System.Net.Dns.GetHostAddresses(iecIp);
                    var ipv4 = System.Linq.Enumerable.FirstOrDefault(addresses, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipv4 != null) 
                    {
                        iecIp = ipv4.ToString();
                        Console.WriteLine($"Resolved to {iecIp}");
                    }
                }
                
                Console.WriteLine($"Initializing IEC104 Client connecting to {iecIp}:{iecPort}");
                iecClientConnection = new Connection(iecIp, iecPort);
                iecClientConnection.SetASDUReceivedHandler(AsduReceivedHandler, null);
                iecClientConnection.SetConnectionHandler(ConnectionHandler, null);
                
                // Keep trying to connect if the server isn't ready
                while (true)
                {
                    try {
                        iecClientConnection.Connect();
                        break;
                    } catch (Exception ex) {
                        Console.WriteLine($"IEC104 Connection failed: {ex.Message}. Retrying in 3s...");
                        Thread.Sleep(3000);
                    }
                }
            }
            else if (mode == "server")
            {
                Console.WriteLine($"Initializing IEC104 Server listening on {iecIp}:{iecPort}");
                iecServer = new Server();
                iecServer.SetLocalPort(iecPort);
                iecServer.Start();
                // A real server would populate a local data model and handle interrogation
                Console.WriteLine("Server started (Listening mode).");
            }
            
            Console.WriteLine("Bridge running. Press Ctrl+C to exit.");
            var exitEvent = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                exitEvent.Set();
            };
            
            exitEvent.Wait();
            Console.WriteLine("Shutting down...");
            if (iecClientConnection != null) iecClientConnection.Close();
            if (iecServer != null) iecServer.Stop();
            await mqttClient.DisconnectAsync();
        }

        /// <summary>
        /// Handles incoming IEC 104 ASDUs, translating them to MQTT payloads.
        /// </summary>
        private static bool AsduReceivedHandler(object parameter, ASDU asdu)
        {
            Console.WriteLine($"[IEC104 -> MQTT] Received ASDU type: {asdu.TypeId}");
            
            for (int i = 0; i < asdu.NumberOfElements; i++)
            {
                var val = asdu.GetElement(i);
                int ioa = val.ObjectAddress;
                string type = "unknown";
                object jsonValue = null;
                bool isInvalid = false;
                
                // Handle Command Confirmations (ACT_CON / ACT_TERM)
                if (val is SingleCommand || val is DoubleCommand || val is SetpointCommandShort || val is SetpointCommandScaled || val is SetpointCommandNormalized)
                {
                    string tid = null;
                    if (pendingTransactions.TryGetValue(ioa, out var storedTid))
                    {
                        tid = storedTid;
                        if (asdu.Cot == CauseOfTransmission.ACTIVATION_TERMINATION || asdu.IsNegative)
                        {
                            pendingTransactions.TryRemove(ioa, out _);
                        }
                    }

                    var statusPayload = JsonSerializer.Serialize(new {
                        ioa = ioa,
                        transaction_id = tid,
                        cot = asdu.Cot.ToString(),
                        isNegative = asdu.IsNegative,
                        typeId = asdu.TypeId.ToString()
                    }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

                    var statusTopic = $"iec104/{bridgeId}/command_status/{ioa}";
                    
                    // Fire and forget so we don't block the lib60870 networking thread!
                    _ = mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                        .WithTopic(statusTopic)
                        .WithPayload(statusPayload)
                        .WithQualityOfServiceLevel(qosLevel)
                        .Build()).ContinueWith(t => 
                        {
                            if (t.IsFaulted) Console.WriteLine($"Failed to publish status: {t.Exception?.GetBaseException().Message}");
                        });
                        
                    Console.WriteLine($"Published Command Status: {statusTopic} -> {statusPayload}");
                    continue;
                }

                // Handle Telemetry (Spontaneous / Interrogated)
                if (val is SinglePointInformation sp)
                {
                    type = "sp";
                    jsonValue = sp.Value;
                    // Quality checking omitted for brevity
                }
                else if (val is DoublePointInformation dp)
                {
                    type = "dp";
                    jsonValue = (int)dp.Value;
                }
                else if (val is StepPositionInformation step)
                {
                    type = "step";
                    jsonValue = step.Value;
                }
                else if (val is MeasuredValueScaled mvs)
                {
                    type = "mv";
                    jsonValue = mvs.ScaledValue;
                }
                else if (val is MeasuredValueShort mvsFloat)
                {
                    type = "mv";
                    jsonValue = mvsFloat.Value;
                }

                if (jsonValue != null)
                {
                    var payload = JsonSerializer.Serialize(new {
                        ioa = ioa,
                        value = jsonValue,
                        quality = new {
                            invalid = isInvalid
                        }
                    });

                    var topic = $"iec104/{bridgeId}/telemetry/{type}/{ioa}";
                    
                    // Fire and forget to avoid deadlocking the lib60870 thread
                    _ = mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(qosLevel)
                        .WithRetainFlag(mqttRetain)
                        .Build()).ContinueWith(t => 
                        {
                            if (t.IsFaulted) Console.WriteLine($"Failed to publish telemetry: {t.Exception?.GetBaseException().Message}");
                        });
                        
                    Console.WriteLine($"Published: {topic} -> {payload}");
                }
            }
            return true;
        }

        /// <summary>
        /// Handles IEC 104 connection state changes (e.g., OPENED, CLOSED).
        /// </summary>
        private static void ConnectionHandler(object parameter, ConnectionEvent connectionEvent)
        {
            Console.WriteLine($"IEC104 Connection Event: {connectionEvent}");
            if (connectionEvent == ConnectionEvent.OPENED)
            {
                Console.WriteLine("Sending Interrogation Command...");
                iecClientConnection.SendInterrogationCommand(CauseOfTransmission.ACTIVATION, 1, 20);
            }
            else if (connectionEvent == ConnectionEvent.CLOSED)
            {
                Console.WriteLine("[WARNING] IEC104 Connection CLOSED by remote peer or network drop.");
            }
        }
    }
}
