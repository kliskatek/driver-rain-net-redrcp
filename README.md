# driver-rain-net-REDRCP

![NuGet Version](https://img.shields.io/nuget/v/Kliskatek.Driver.Rain.REDRCP)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kliskatek.Driver.Rain.REDRCP.svg)](https://www.nuget.org/packages/Kliskatek.Driver.Rain.REDRCP/)

Provides high level methods that give access to RED [Reader Control Protocol (RCP)](https://www.phychips.com/upload/board/Reader_Control_Protocol_User_Manual.pdf)  commands, responses and notifications.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
  - [Connect to the reader](#connect-to-the-reader)
  - [Disconnect from reader](#disconnect-from-reader)
  - [RCP protocol command wrapping functions](#rcp-protocol-command-wrapping-functions)
  - [Obtaining command failure error codes](#obtaining-command-failure-error-codes)
  - [Notification event handling](#notification-event-handling)
  - [Error event handling](#error-event-handling)
  - [Perform asynchronous inventory](#perform-asynchronous-inventory)
  - [Basic Read and Write operations](#basic-read-and-write-operations)

- [License](#license)


## Installation

The library is installed from [NuGet](https://www.nuget.org/packages/Kliskatek.Driver.Rain.REDRCP)

```
dotnet add package Kliskatek.Driver.Rain.REDRCP
```

## Usage
### Connect to the reader

The transport layer of the library is decoupled from the RCP wrapper, although currently only serial port communication is supported. To connect to the reader, the user has to provice the name of the serial port interface 

```csharp
var reader = new REDRCP();
string connectionString = "COM4";
if (reader.Connect(connectionString))
	Console.WriteLine("Reader connected");
```

If the user needs to further configure the parameters of the serial port, it is possible to serialize the provided ***SerialPortConnectionParameters*** data class into a JSON string and pass is to the reader's Connect method

```csharp
using Newtonsoft.Json;

var reader = new REDRCP();
string connectionString = JsonConvert.SerializeObject(new SerialPortConnectionParameters
{
    PortName = "COM4",
    BaudRate = 115200
});
if (reader.Connect(connectionString))
    Console.WriteLine("Reader connected");
```

### Disconnect from reader

To disconnect from the reader, simply call the following method:
```csharp
if (reader.Disconnect())
    Console.WriteLine("Disconnected from reader");
```

### RCP protocol command wrapping functions
All RCP protocol function wrappers defined in the driver return a type defined in the *** *** enum
```csharp
public enum RcpResultType
{
    Success,
    ReaderError,
    NoResponse,
    OtherError
}
```
_Success_ return types indicates the reader completed the command successfully. If the RCP function returns some data, this data will be returned as an _out_ parameters of the corresponding driver wrapping function. As an example, here is the signature of the driver function wrapping RCP function _Get Region_ defined in section 4.3:

```csharp
public RcpResultType GetRegion(out Region region)
```
_ReaderError_ indicates that the reader reported a command failure conditions. _NoReponse_ indicates that the reader timed out a RCP command. Other errors the reader may encounter during operation are indicated with _OtherError_.

It is possible to use the driver without checking the return value of any RCP function wrapper.

```csharp
reader.GetRegion(out var region);
Console.WriteLine($"Reader region : {region}");
```

However, it is recommended the user to check the RCP function wrappers' return values
```csharp
string epc = "1234567890ABCDEF";
ushort startAddress = 5;
ushort wordCount = 2;
switch (reader.ReadTypeCTagData(epc, ParamMemoryBank.User, startAddress, wordCount, out var readData))
{
    case RcpResultType.Success:
        Console.WriteLine($"Tag data : {readData}");
        break;
    case RcpResultType.ReaderError:
        // readData contains no valid data
        // Code to handle command failure 
        break;
    case RcpResultType.NoResponse:
        // readData contains no valid data
        // Code to handle timeout condition during RCP command processing
        break;
    case RcpResultType.OtherError:
        // readData contains no valid data
        // Other errors conditions that are not treated as RCP command failures
        break;
}
```

### Obtaining command failure error codes

When a command failure occurs, it is possible request the driver to get the last recorder command failure error code, or to request the last command failure error code occurred when processing a given RCP function

```csharp
string epc = "1234567890ABCDEF";
ushort startAddress = 5;
ushort wordCount = 2;
switch (reader.ReadTypeCTagData(epc, ParamMemoryBank.User, startAddress, wordCount, out var readData))
{
    case RcpResultType.ReaderError:
        // Option 1: Get last error code registered by reader
        var lastErrorCode = reader.GetLastError();
        // Option 2: Get last error code registered by reader associated with RCP command
        var rcpCode = MessageCode.ReadTypeCTagData;
        if (reader.TryGetCommandErrorCode(rcpCode, out var errorCode))
            Console.WriteLine($"RCP command {rcpCode} returned error [{(byte)errorCode}] {errorCode}");
        // User error handling
        break;
    default:
        break;
}
```

The driver also provides an event delegate to asynchronously handle error conditions. This capability will be explained in section [Error event handling](#error-event-handling)

### Notification event handling

Some RCP commands return data asynchronously using notification. The driver provides an event delegate that informs the user of such notifications
```csharp
reader.NewNotificationReceived += NewNotificationReceived;

public static void NewNotificationReceived(object sender, NotificationEventArgs e)
{

}
```

The data returned by the RCP notification depends on its code. TO be able to handle all notifications with a single callback method, class _NotificationEventArgs_ includes contains command code of the notification and its data parameters wrapped in a _object_:
```csharp
public class NotificationEventArgs : EventArgs
{
    public SupportedNotifications NotificationType { get; set; }
    public object NotificationParameters { get; set; }
}
```

It is the responsability of the _NewNotificationReceived_ handler to unwrap the notification data according to the notification code. The following snippet shows an example of how to handle all supported RCP notifications supported by the driver:
```csharp
reader.NewNotificationReceived += NewNotificationReceived;

public static void NewNotificationReceived(object sender, NotificationEventArgs e)
{
    switch (e.NotificationType)
    {
        case SupportedNotifications.ReadTypeCUii:
            OnReadTypeCUiiNotification((ReadTypeCUiiNotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.ReadTypeCUiiTid:
            OnReadTypeCUiiTidNotification((ReadTypeCUiiTidNotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.ReadTypeCUiiRssi:
            OnReadTypeCUiiRssiNotification((ReadTypeCUiiRssiNotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.StartAutoReadRssi:
            OnStartAutoReadRssiNotification((StartAutoReadRssiNotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.ReadTypeCUiiEx2:
            OnReadTypeCUiiEx2Notification((ReadTypeCUiiEx2NotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.StartAutoRead2Ex:
            OnStartAutoRead2ExNotification((StartAutoRead2ExNotificationParameters)e.NotificationParameters);
            break;
        case SupportedNotifications.GetDtcResult:
            OnGetDtcResult((GetDtcResultNotificationParameters)e.NotificationParameters);
            break;
        default:
            Console.WriteLine($"Notification {e.NotificationType} not supported yet");
            break;
    }
}

public static void OnReadTypeCUiiNotification(ReadTypeCUiiNotificationParameters parameters) { }
public static void OnReadTypeCUiiTidNotification(ReadTypeCUiiTidNotificationParameters parameters) { }
public static void OnReadTypeCUiiRssiNotification(ReadTypeCUiiRssiNotificationParameters parameters) { }
public static void OnStartAutoReadRssiNotification(StartAutoReadRssiNotificationParameters parameters) { }
public static void OnReadTypeCUiiEx2Notification(ReadTypeCUiiEx2NotificationParameters parameters) { }
public static void OnStartAutoRead2ExNotification(StartAutoRead2ExNotificationParameters parameters) { }
public static void OnGetDtcResult(GetDtcResultNotificationParameters parameters) { }
```

### Error event handling
The driver provides an event delegate that informs the user when a RCP command failure occurs:
```csharp
reader.NewErrorReceived += NewErrorReceived;

public static void NewErrorReceived(object sender, ErrorNotificationEventArgs e)
{
    Console.WriteLine($"Command {e.CommandCode} returned an error: [{(byte)e.ErrorCode}] {e.CommandCode}");
}
```

### Perform asynchronous inventory
The following code snippet shows and example of how to perform an inventory round with the driver:

```csharp
reader.NewNotificationReceived += NewNotificationReceived;

reader.StartAutoRead2();
Thread.Sleep(2000);
reader.StopAutoRead2();

public static void NewNotificationReceived(object sender, NotificationEventArgs e)
{
    switch (e.NotificationType)
    {
        case SupportedNotifications.ReadTypeCUii:
            OnReadTypeCUiiNotification((ReadTypeCUiiNotificationParameters)e.NotificationParameters);
            break;
        default:
            break;
    }
}

public static void OnReadTypeCUiiNotification(ReadTypeCUiiNotificationParameters parameters)
{
    Console.WriteLine("ReadTypeCUii notification received");
    Console.WriteLine($" * [{parameters.Pc}] EPC = {parameters.Epc}\n");
}
```
### Basic Read and Write operations

The following snippet shows how to read a tag, given an EPC, memory bank, start address and word count:

```csharp
string epc = "1234567890ABCDEF";
ushort startAddress = 5;
ushort wordCount = 2;
switch (reader.ReadTypeCTagData(epc, ParamMemoryBank.User, startAddress, wordCount, out var readData))
{
    case RcpResultType.Success:
        Console.WriteLine($"Tag data : {readData}");
        break;
    default:
        break;
}
```

The following snippet shows how the write to a tag, given 
```csharp
string epc = "1234567890ABCDEF";
ushort startAddress = 5;
string dataToWrite = "FEDCBA0987654321";
switch (reader.WriteTypeCTagData(epc, ParamMemoryBank.User, startAddress, dataToWrite))
{
    case RcpResultType.Success:
        Console.WriteLine("Tag written successfully");
        break;
    default:
        break;
}
```

## License

driver-rain-net-REDRCP is distributed under the terms of the [MIT](https://spdx.org/licenses/MIT.html) license.

