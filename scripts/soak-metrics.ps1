function Get-SoakAcceptance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Samples,
        [double]$GrowthWindowHours = 4,
        [double]$MaximumAverageCpuPercent = 1,
        [double]$MaximumStableWorkingSetMb = 150,
        [double]$MaximumWorkingSetGrowthMb = 20
    )

    if ($Samples.Count -lt 2) {
        throw "At least two samples are required."
    }

    $ordered = @($Samples | Sort-Object { [double]$_.ElapsedSeconds })
    $averageCpu = [double](($ordered | Measure-Object CpuPercent -Average).Average)
    $warmupCount = [Math]::Floor($ordered.Count * 0.1)
    $stable = @($ordered | Select-Object -Skip $warmupCount)
    $stableWorkingSet = [double](($stable | Measure-Object WorkingSetMb -Average).Average)

    $lastElapsed = [double]$ordered[-1].ElapsedSeconds
    $windowSeconds = [Math]::Max(0, $GrowthWindowHours * 3600)
    $windowStart = [Math]::Max(
        [double]$ordered[0].ElapsedSeconds,
        $lastElapsed - $windowSeconds)
    $growthSamples = @($ordered | Where-Object {
        [double]$_.ElapsedSeconds -ge $windowStart
    })

    $meanX = [double](($growthSamples | Measure-Object ElapsedSeconds -Average).Average)
    $meanY = [double](($growthSamples | Measure-Object WorkingSetMb -Average).Average)
    $numerator = 0.0
    $denominator = 0.0
    foreach ($sample in $growthSamples) {
        $x = [double]$sample.ElapsedSeconds - $meanX
        $y = [double]$sample.WorkingSetMb - $meanY
        $numerator += $x * $y
        $denominator += $x * $x
    }

    $observedSeconds = [Math]::Max(
        0,
        [double]$growthSamples[-1].ElapsedSeconds -
            [double]$growthSamples[0].ElapsedSeconds)
    $growth = if ($denominator -eq 0) {
        0.0
    }
    else {
        ($numerator / $denominator) * $observedSeconds
    }

    $cpuPassed = $averageCpu -lt $MaximumAverageCpuPercent
    $workingSetPassed = $stableWorkingSet -lt $MaximumStableWorkingSetMb
    $growthPassed = $growth -lt $MaximumWorkingSetGrowthMb

    [pscustomobject]@{
        Passed = $cpuPassed -and $workingSetPassed -and $growthPassed
        AverageCpuPercent = [Math]::Round($averageCpu, 3)
        StableWorkingSetMb = [Math]::Round($stableWorkingSet, 3)
        WorkingSetGrowthMb = [Math]::Round($growth, 3)
        CpuPassed = $cpuPassed
        WorkingSetPassed = $workingSetPassed
        GrowthPassed = $growthPassed
        SampleCount = $ordered.Count
    }
}
