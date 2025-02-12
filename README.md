# MQTT To AIDA64 Bridge

## Release Here -> [V1.0.0](https://github.com/ghostseven/MQTT-To-AIDA64-Bridge/releases/tag/v1.0.0)

A bridge between a local MQTT server and AIDA64, it utilises AIDAs [custom registry values](https://www.aida64.com/user-manual/hardware-monitoring/displaying-custom-values) to read from MQTT and write to these registry fields. 

Run the program at startup, and it will show a little icon in the notification area (a red mqtt icon), you can right click it to exit.  

You can use up to 10 DWORD (32 bit integer) values and up to 10 String values. 

Once data has been capture and written to the registry any items will show up as Registry Value (NAME OF REG) when you add a new sensor to the sensor pannel. 

![](reg-values-in-aida.png) 

There is a simple config in JSON that allows you to define a MQTT broker and optionally a username and or password for the broker. You can then define up to 20 keys (10 DWORD, 10 String) to read different MQTT paths to the registry keys.

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

In the example above, I am loging on to a local MQTT server with the ip 1.1.1.1 and a user name of 'user' and a password of 'password'. 

I then define two DWORD keys (key can be either DW1-10 or STR1-10) to read from two MQTT topics (they happen to be the same but can be different), 
you can optionally (as I do above) specify a path, this allows you to walk along any returned JSON data from the topic to pick out a value.  

The topic draytronics/switch/game_pc/SENSOR returns 

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

So I use the path ENERGY.Power and ENERGY.Voltage to return those values rather than the whole JSON block.  If your topic returns a simple value you can exclude the path part or set it to an empty value.

If you are using a DW1-10 key the program will try and parse the response as an integer and will not set the value if it fails, for strings or any non integer values use a STR1-10 key.


*This is a very early and very untested version, this has just been used by me to get values from a Tasmota power plug to display the overall power usage in AIDA64, more than likely this will have bugs (a am happy to fix them) so be warned!*