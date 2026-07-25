$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "..\..\scripts\soak-metrics.ps1")

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message; expected: $Expected; actual: $Actual"
    }
}

$samples = @(
    [pscustomobject]@{ ElapsedSeconds = 0; CpuPercent = 0.2; WorkingSetMb = 100 },
    [pscustomobject]@{ ElapsedSeconds = 30; CpuPercent = 0.4; WorkingSetMb = 102 },
    [pscustomobject]@{ ElapsedSeconds = 60; CpuPercent = 0.3; WorkingSetMb = 104 }
)
$result = Get-SoakAcceptance -Samples $samples -GrowthWindowHours (1 / 60)

Assert-Equal $true $result.Passed "Healthy samples should pass"
Assert-Equal 0.3 $result.AverageCpuPercent "Average CPU should be correct"
Assert-Equal 4.0 $result.WorkingSetGrowthMb "Working set growth should be correct"

$highCpu = $samples | ForEach-Object {
    [pscustomobject]@{
        ElapsedSeconds = $_.ElapsedSeconds
        CpuPercent = 1.2
        WorkingSetMb = $_.WorkingSetMb
    }
}
Assert-Equal $false (Get-SoakAcceptance -Samples $highCpu).Passed "High CPU should fail"

$growing = @(
    [pscustomobject]@{ ElapsedSeconds = 0; CpuPercent = 0.1; WorkingSetMb = 100 },
    [pscustomobject]@{ ElapsedSeconds = 30; CpuPercent = 0.1; WorkingSetMb = 115 },
    [pscustomobject]@{ ElapsedSeconds = 60; CpuPercent = 0.1; WorkingSetMb = 125 }
)
Assert-Equal $false (Get-SoakAcceptance -Samples $growing -GrowthWindowHours (1 / 60)).Passed "Continuous growth should fail"

Write-Host "Soak metric tests passed: 3 groups"
