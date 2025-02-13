# MQTT To AIDA64 Bridge

## Download Release Here -> [Releases](https://github.com/ghostseven/MQTT-To-AIDA64-Bridge/releases/)

A bridge between a local MQTT server and AIDA64, it utilises AIDAs [custom registry values](https://www.aida64.com/user-manual/hardware-monitoring/displaying-custom-values) to read from MQTT and write to these registry fields.

## Installation

- To install the bridge, please go to the [release page](https://github.com/ghostseven/MQTT-To-AIDA64-Bridge/releases/) and grab the latest version (it will be a zip file of the compiled app).

- Unzip the application to a suitable directory on your system.

- It is advised to run the program as startup, so it starts with or shortly after AIDA64.

- When the program is running it will show an icon in the notification area (a red mqtt icon), you can right click it to exit.

## Configuration

There is a simple config in JSON that allows you to define a MQTT broker and optionally a username and or password for the broker. You can then define up to 20 keys (10 DWORD, 10 String) to read different MQTT paths to the registry keys.

An example config, shown here is included in the release, it will require some adjustment to work with your local setup.

    {
      "broker": {
        "host": "1.1.1.1",
        "user": "user",
        "password": "password"
      },
      "keys": [
        {
          "key": "DW1",
          "topic": "draytronics/switch/game_pc/SENSOR",
          "path": "ENERGY.Power"
        },
        {
          "key": "DW2",
          "topic": "draytronics/switch/game_pc/SENSOR",
          "path": "ENERGY.Voltage"
        }
      ]
    }

### Broker Section

The 'broker' section defines how you connect to your local MQTT server.

`host` - This is the IP address or DNS name of your MQTT server

`user` - This is the user name needed to connect to your MQTT server (optional)

`password` - This is the password needed to connect to your MQTT server (optional)

### Keys Section

The 'keys' section defines what topics you are going to subscribe to on your MQTT server, what you will do with the returned data and what registry key it should be written to.

`key` - This is the name of the key that should be written in the AIDA64 [custom registry values](https://www.aida64.com/user-manual/hardware-monitoring/displaying-custom-values) these can only be DW1 to DW10 or STR1 to STR10 as defined by the linked page. DW keys are DWORD (32 bit integer values) STR keys are String.

`topic` - This is the topic to subscribe to on the MQTT server

`path` - This is the path value that allows you to pick out specific JSON nodes if the data returned from the topic is JSON (more detail of this under here). If your topic does not return JSON but a simple value you can exclude adding the path value or set it to an empty value.

To expand on the `path` setting, in the example config above the topic draytronics/switch/game_pc/SENSOR returns;

    {
      "Time": "2025-02-12T22:47:39",
      "ENERGY": {
        "TotalStartTime": "2025-02-10T16:47:09",
        "Total": 0.904,
        "Yesterday": 0.636,
        "Today": 0.258,
        "Power": 122,
        "ApparentPower": 126,
        "ReactivePower": 33,
        "Factor": 0.96,
        "Voltage": 243,
        "Current": 0.521
      }
    }

So I use the path ENERGY.Power and ENERGY.Voltage to return those values rather than the whole JSON block. If you have an array in your JSON data you can also do paths that include indexes such as item[3].value.

### Notes On Interger Parsing

If you are using a DW1-10 key the program will try and parse the response as an integer and will not set the value if it fails, for strings or any non integer values use a STR1-10 key.

## Using Data In AIDA64

Once data has been capture and written to the registry any items will show up as Registry Value (NAME OF REG) when you add a new sensor to the sensor panel.

![](reg-values-in-aida.png)

## WARNING

_This is a very early and very untested version, this has just been used by me to get values from a Tasmota power plug to display the overall power usage in AIDA64, more than likely this will have bugs (a am happy to fix them) so be warned!_
