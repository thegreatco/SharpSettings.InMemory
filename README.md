# SharpSettings
An InMeory implementation of SharpSettings. This supports storing and querying settings from a shared in-memory collection.

| dev | master |
| --- | ------ |
| [![CircleCI](https://circleci.com/gh/thegreatco/SharpSettings.InMemory/tree/dev.svg?style=svg)](https://circleci.com/gh/thegreatco/SharpSettings.InMemory/tree/dev) | [![CircleCI](https://circleci.com/gh/thegreatco/SharpSettings.InMemory/tree/master.svg?style=svg)](https://circleci.com/gh/thegreatco/SharpSettings.InMemory/tree/master) |

See [SharpSettings](https://github.com/thegreatco/SharpSettings) for general usage instructions.
# Usage

WIP

### Logger
To be as flexible as possible and not requiring a particular logging framework, a shim must be implemented that implements the `ISharpSettingsLogger` interface. It follows similar patterns to `Serilog.ILogger` but is easily adapted to `Microsoft.Extensions.Logging` as well.