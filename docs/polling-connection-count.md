# Polling Connection Count

It is possible to control the number of RPCs a Polling Tentacle can execute concurrently.
 
With a value of just `1`, a Polling Tentacle that is sending a large file will be prevented 
from doing anything else while the file is being transferred. While this works for Polling Tentacle
Targets which generally do one thing at a time, it doesn't work well for Polling Tentacle Workers since they tend to
be given multiple requests to execute at the same time. 

## Defaults

Registering with `register-worker` auto-sets the count to `max(5, processor count)` if it hasn't already been set. Deployment targets default to 1. Only applies to polling registrations.
 
## How to set it for existing instances.

- `Tentacle set-polling-connection-count --instance MyInstance --pollingConnectionCount 5`
- Directly in the Tentacle configuration XML:

```xml
<set key="Tentacle.Communication.PollingConnectionCount">5</set>
```

## Limits

The value is coerced to `1 <= count <= 512`.

## Docker

Set the `PollingConnectionCount` environment variable on the container. See [docker/readme.md](../docker/readme.md).
