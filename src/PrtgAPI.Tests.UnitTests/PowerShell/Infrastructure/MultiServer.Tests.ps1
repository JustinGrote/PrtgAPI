. $PSScriptRoot\..\..\Support\PowerShell\Standalone.ps1

Describe "Multi-Server" -Tag @("PowerShell", "UnitTest") {

    It "aggregates results across multiple clients by default" {
        Disconnect-PrtgServer

        # Create two mocked clients with different servers
        $resp1 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client1 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg1.example.com", $resp1)
        Set-PrtgClient $client1

        $resp2 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client2 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg2.example.com", $resp2)
        Set-PrtgClient $client2

        # Now default operations Should -hit both servers and combine results
        $sensors = Get-Sensor
        $sensors.Count | Should -Be 2

        # Verify server affinity tagging
        ($sensors | Where-Object { $_.PSPrtgServer -eq "prtg1.example.com" }).Count | Should -Be 1
        ($sensors | Where-Object { $_.PSPrtgServer -eq "prtg2.example.com" }).Count | Should -Be 1
    }

    It "filters operations to specified client via -Client" {
        Disconnect-PrtgServer

        $resp1 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client1 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg1.example.com", $resp1)
        Set-PrtgClient $client1

        $resp2 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client2 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg2.example.com", $resp2)
        Set-PrtgClient $client2

        $sensors = Get-Sensor -Client "prtg1.example.com"
        $sensors.Count | Should -Be 1
        $sensors[0].PSPrtgServer | Should -Be "prtg1.example.com"
    }

    It "Get-PrtgClient returns multiple clients and can filter by server" {
        Disconnect-PrtgServer

        $resp1 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client1 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg1.example.com", $resp1)
        Set-PrtgClient $client1

        $resp2 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client2 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg2.example.com", $resp2)
        Set-PrtgClient $client2

        $clients = @(Get-PrtgClient)
        $clients.Count | Should -Be 2
        ($clients | Select-Object -ExpandProperty Server | Sort-Object)[0] | Should -Be "prtg1.example.com"
        ($clients | Select-Object -ExpandProperty Server | Sort-Object)[1] | Should -Be "prtg2.example.com"

        $client = Get-PrtgClient -Server "prtg1.example.com"
        $client.Server | Should -Be "prtg1.example.com"
    }

    It "Disconnect-PrtgServer can disconnect a specific server" {
        Disconnect-PrtgServer

        $resp1 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client1 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg1.example.com", $resp1)
        Set-PrtgClient $client1

        $resp2 = New-Object PrtgAPI.Tests.UnitTests.Support.TestResponses.MultiTypeResponse
        $client2 = [PrtgAPI.Tests.UnitTests.BaseTest]::Initialize_Client_WithServer("prtg2.example.com", $resp2)
        Set-PrtgClient $client2

        Disconnect-PrtgServer -Server "prtg1.example.com"

        $clients = @(Get-PrtgClient)
        $clients.Count | Should -Be 1
        $clients[0].Server | Should -Be "prtg2.example.com"
    }

    It "Connect-PrtgServer can connect to multiple servers" {
        Disconnect-PrtgServer
        $cred = New-Credential prtgadmin 12345678

        $clients = @(Connect-PrtgServer prtg1.example.com,prtg2.example.com $cred -PassHash -PassThru -Force)
        $clients.Count | Should -Be 2

        # Cleanup
        Disconnect-PrtgServer
    }
}
