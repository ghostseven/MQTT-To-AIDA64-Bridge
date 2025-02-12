using Microsoft.Win32;
using MQTTnet;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace MQTT_To_AIDA64_Bridge
{
    class Program
    {
        const string AID64RegPath = @"Software\FinalWire\AIDA64\ImportValues";

        private static NotifyIcon? trayIcon;
        private static IMqttClient? mqttClient;
        private static string? mqttConfig;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            //Setup defaults for rending just a tray icon application without a form. 
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            trayIcon = new NotifyIcon
            {
                Icon = new Icon("tray.ico"),
                ContextMenuStrip = new ContextMenuStrip()
            };
            //Add an exit right click menu item 
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApplication());
            trayIcon.Text = "MQTT To AIDA64 Bridge";
            trayIcon.Visible = true;

            //Task out to the MQTT connection and have that run in the background.
            Task.Run(ConnectToMqtt);
            //Start the app. 
            Application.Run();
        }

        private static async Task ConnectToMqtt()
        {
            //Broker and config globals.
            string? mqttBroker = null;
            string? mqttUser = null;
            string? mqttPass = null;
            JsonNode root = null;

            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"config.json")))// If we can load the config file and it parses out sensible values continue, if not close the application. 
            {
                try
                {
                    mqttConfig = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"));
                    root = JsonNode.Parse(mqttConfig);
                    
                    mqttBroker = GetJsonValue(root, "broker.host");
                    mqttUser = GetJsonValue(root, "broker.user");
                    mqttPass = GetJsonValue(root, "broker.password");
                }
                catch 
                {
                    MessageBox.Show("Failure to parse config file MQTT broker settings, please check config.json", "Error - MQTT Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ExitApplication();
                }
            }
            else
            {
                MessageBox.Show("Unable to find config.json, please check file exists and is readable by user.", "Error - Config Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApplication();
            }
            //Create mqtt factory.
            var factory = new MqttClientFactory();
            mqttClient = factory.CreateMqttClient();

            //Make sure broker is not null.
            if (mqttBroker == null) {
                MessageBox.Show("Broker is null, closing application.", "Error - Broker Is Null", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                ExitApplication();
            }

            //Start building client options, intially add with no user or pass then check and adjust as per config params
            MqttClientOptions options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBroker)
                .Build();

            if (mqttUser != null) //Have user build out with that
            {
                options = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttBroker)
                    .WithCredentials(mqttUser, mqttPass)
                    .Build();
                if (mqttPass != null) { //Have pass as well build out with both
                    options = new MqttClientOptionsBuilder()
                        .WithTcpServer(mqttBroker)
                        .WithCredentials(mqttUser, mqttPass)
                        .Build();
                }
            }


            //Start MQTT connect. 
            mqttClient.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected to MQTT broker!");
                List<string> subscriptions = []; 

                //AIDA64 Has a maximum of 20 custom registry key values 10 DWord and 10 String, loop over max they will return null if unpopulated in config.
                for (int i = 0; i< 20; i++)
                {
                    string? topic = GetJsonValue(root, $"keys[{i}].topic");
                    if (topic!=null)
                    {
                        AddUnique(subscriptions, topic); //store subsciptions (avoiding duplicates).
                    }
                }
                //Loop over each subscription and subscribe. 
                foreach(string subscription in subscriptions)
                {
                    await mqttClient.SubscribeAsync(subscription);
                }
            };

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string payload = e.ApplicationMessage.ConvertPayloadToString();
                Console.WriteLine($"Message received: {payload}");

                List<KeyInfo> matches = GetKeysByTopic(root, e.ApplicationMessage.Topic);

                //Simple regex to match if key values are in the format required for AIDA64 registry keys.
                string pattern = @"^(DW[1-9]|DW10|STR[1-9]|STR10)$";
                Regex regKeyRegex = new Regex(pattern, RegexOptions.IgnoreCase);

                foreach (KeyInfo match in matches)
                {

                    if (regKeyRegex.IsMatch(match.Key))
                    {
                        //If path is null or empty we just need to grab the value that is returned (so the payload as is).
                        if (String.IsNullOrEmpty(match.Path))
                        {
                            if (match.Key.StartsWith("dw", System.StringComparison.OrdinalIgnoreCase)) //we have a dword value (integer).
                            {
                                //If we have a dword, try and parse as an integer.
                                if (int.TryParse(payload, out int i))
                                {
                                    //Set the DWORD registry key.
                                    Registry.SetValue(Path.Combine(Registry.CurrentUser.Name, AID64RegPath), match.Key.ToUpper(), i, RegistryValueKind.DWord);
                                }
                            }
                            else //Consider anything else a string.
                            {
                                //Set the String registry key
                                Registry.SetValue(Path.Combine(Registry.CurrentUser.Name, AID64RegPath), match.Key.ToUpper(), payload, RegistryValueKind.String);
                            }
                        }
                        else //If path is populated we need to process the returned payload as JSON and walk the path.
                        {
                            JsonNode rJson = JsonNode.Parse(payload);
                            string? jsonValue = GetJsonValue(rJson, match.Path);

                            if (jsonValue != null) //If we do not get a null value back continue.
                            {
                                if (match.Key.StartsWith("dw", System.StringComparison.OrdinalIgnoreCase)) //we have a dword value (integer).
                                {
                                    //If we have a dword, try and parse as an integer.
                                    if(int.TryParse(jsonValue, out int i))
                                    {
                                        //Set the DWORD registry key.
                                        Registry.SetValue(Path.Combine(Registry.CurrentUser.Name, AID64RegPath), match.Key.ToUpper(), i, RegistryValueKind.DWord);
                                    }
                                }
                                else //Consider anything else a string
                                {
                                    //Set the String registry key
                                    Registry.SetValue(Path.Combine(Registry.CurrentUser.Name, AID64RegPath), match.Key.ToUpper(), jsonValue, RegistryValueKind.String);
                                }
                            }
                        }
                    }
                }
                return Task.CompletedTask;
            };

            mqttClient.DisconnectedAsync += async e =>
            {
                Console.WriteLine("Disconnected from MQTT broker. Reconnecting...");
                await Task.Delay(5000);
                await mqttClient.ConnectAsync(options);
            };

            try
            {
                await mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MQTT connection error: {ex.Message}");
            }
        }

        //This function is used to pull out a node value, given a JsonNode and a path, this allows us to dynamically process JSON.
        static string? GetJsonValue(JsonNode node, string path)
        {
            string[] keys = path.Split('.');
            foreach (string key in keys)
            {
                //Process array items so we can have a path with things like key[0].someItem
                var match = Regex.Match(key, @"(\w+)\[(\d+)\]");
                if (match.Success)
                {
                    string arrayKey = match.Groups[1].Value;
                    int index = int.Parse(match.Groups[2].Value);

                    if (node is JsonObject obj && obj.ContainsKey(arrayKey))
                        node = obj[arrayKey];

                    if (node is JsonArray arr && index < arr.Count)
                        node = arr[index];
                    else
                        return null;
                }
                else if (node is JsonObject obj && obj.ContainsKey(key))
                {
                    node = obj[key];
                }
                else
                {
                    return null; //Always return null if we don't match so we can process this without failure. 
                }
            }
            return node?.ToString();
        }

        //Simple class to store the key pairs, so we can itterate over them from the config file. 
        class KeyInfo
        {
            public string Key { get; set; }
            public string Topic { get; set; }
            public string Path { get; set; }
        }

        //A function to search the keys in the config file by topic, this allows us to subsribe once to a topic even if we use it multiple times, then return the keys that match that topic when we get a MQTT message.
        static List<KeyInfo> GetKeysByTopic(JsonNode root, string topic)
        {
            List<KeyInfo> matchingKeys = new List<KeyInfo>();
            JsonNode? keysNode = root["keys"];

            if (keysNode is JsonArray keysArray)
            {
                foreach (JsonNode? item in keysArray)
                {
                    if (item is JsonObject obj)
                    {
                        string? key = obj["key"]?.ToString();
                        string? itemTopic = obj["topic"]?.ToString();
                        string? path = obj["path"]?.ToString();

                        if (itemTopic == topic && key != null && path != null)
                        {
                            matchingKeys.Add(new KeyInfo
                            {
                                Key = key,
                                Topic = itemTopic,
                                Path = path
                            });
                        }
                    }
                }
            }

            return matchingKeys;
        }

        //Only add unique keys to a list of strings, ingnoring case. 
        static void AddUnique(List<string> list, string newItem)
        {
            if (!list.Contains(newItem, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(newItem);
            }
        }

        //Exit and clean up.
        private static void ExitApplication()
        {
            trayIcon!.Visible = false;
            mqttClient?.DisconnectAsync();
            Application.Exit();
        }
    }
}