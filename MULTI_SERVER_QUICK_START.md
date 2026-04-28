# Multi-Server Quick Start Guide

## Connecting to Multiple Servers

### Connect to multiple servers with the same credentials:
```powershell
Connect-PrtgServer prtg1.example.com,prtg2.example.com (Get-Credential)
```

### Connect to servers one at a time:
```powershell
Connect-PrtgServer prtg1.example.com (Get-Credential)
Connect-PrtgServer prtg2.example.com (Get-Credential)
```

## Working with Multiple Servers

### Query all connected servers (default behavior):
```powershell
# Gets sensors from ALL connected servers in parallel
Get-Sensor
```

### Query specific server(s):
```powershell
# Get sensors from only one server
Get-Sensor -Client "prtg1.example.com"

# Get sensors from multiple specific servers
Get-Sensor -Client "prtg1.example.com","prtg2.example.com"

# Store client and use it
$client = Get-PrtgClient -Server "prtg1.example.com"
Get-Sensor -Client $client
```

### Pipeline operations respect server affinity:
```powershell
# Each sensor knows which server it came from
# Set-ObjectProperty automatically operates on the correct server
Get-Sensor | Set-ObjectProperty -Property Active -Value $false

# This works even with mixed objects from different servers
Get-Sensor | Where-Object Status -eq "Down" | Resume-Object
```

## Managing Connections

### View all connected servers:
```powershell
Get-PrtgClient
```

### View specific connection:
```powershell
Get-PrtgClient -Server "prtg1.example.com"
```

### Disconnect from specific server:
```powershell
Disconnect-PrtgServer -Server "prtg1.example.com"
```

### Disconnect from all servers:
```powershell
Disconnect-PrtgServer
```

## Advanced Scenarios

### Working with specific objects from specific servers:
```powershell
# Connect to multiple servers
Connect-PrtgServer server1,server2 $cred

# Get a specific sensor from server1
$sensor = Get-Sensor -Client server1 -Id 2001

# This operation will only affect server1 because $sensor came from server1
$sensor | Set-ObjectProperty -Property Active -Value $false
```

### Comparing data across servers:
```powershell
Connect-PrtgServer prtg-prod.example.com,prtg-dev.example.com $cred

# Get all sensors from both servers
$allSensors = Get-Sensor

# Group by source server using the PSPrtgServer property
$allSensors | Group-Object { $_.PSPrtgServer }

# Compare counts
$prodSensors = Get-Sensor -Client "prtg-prod.example.com"
$devSensors = Get-Sensor -Client "prtg-dev.example.com"

Write-Host "Production sensors: $($prodSensors.Count)"
Write-Host "Development sensors: $($devSensors.Count)"
```

### Processing servers in sequence instead of parallel:
```powershell
# Get clients individually and process them
$clients = Get-PrtgClient

foreach ($client in $clients) {
    Write-Host "Processing $($client.Server)..."
    $sensors = Get-Sensor -Client $client
    # Do something with sensors
}
```

## Important Notes

### Automatic Parallel Execution
- When you don't specify `-Client`, operations run against ALL servers in parallel
- This is efficient but be aware of the combined load on your servers
- Results are automatically combined

### Server Affinity
- Objects remember which server they came from
- Piping objects between cmdlets preserves this relationship
- You don't need to specify `-Client` for piped objects

### Backward Compatibility
- Single-server scripts work exactly as before
- No changes needed to existing code
- Multi-server features are opt-in

### Error Handling
- If one server fails, operations on other servers continue
- Check for errors on a per-operation basis
- Use Try/Catch blocks as normal

## Common Patterns

### Pattern 1: Apply configuration to all servers
```powershell
Connect-PrtgServer server1,server2,server3 $cred

# This runs on all servers
Get-Sensor -Status Down | Resume-Object
```

### Pattern 2: Query all, operate on one
```powershell
Connect-PrtgServer server1,server2 $cred

# Get from all servers
$downSensors = Get-Sensor -Status Down

# Only resume sensors from server1
$downSensors | Where-Object { $_.PSPrtgServer -eq "server1" } | Resume-Object
```

### Pattern 3: Different operations per server
```powershell
Connect-PrtgServer prod,dev $cred

# Get different sensors from each
$prodCritical = Get-Sensor -Client prod -Priority 5
$devAll = Get-Sensor -Client dev

# Process differently
$prodCritical | Send-NotificationEmail
$devAll | Export-Csv dev-sensors.csv
```

## Troubleshooting

### Check which server an object came from:
```powershell
$sensor = Get-Sensor -Id 2001
$sensor.PSPrtgServer  # Shows the server URL
```

### Verify connections:
```powershell
Get-PrtgClient | Format-Table Server, UserName, RetryCount
```

### Force reconnection:
```powershell
Connect-PrtgServer server1 $cred -Force
```

### Clear all connections:
```powershell
Disconnect-PrtgServer
# Verify
if (-not (Get-PrtgClient)) {
    Write-Host "All connections cleared"
}
```
