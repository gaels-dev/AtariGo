param(
    [Parameter(Mandatory)]
    [string]$ReportTaskPath,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$sonarBaseUrl = 'https://sonarcloud.io'
$projectKey = $env:SONAR_PROJECT_KEY
$sonarToken = $env:SONAR_TOKEN
$pullRequestNumber = $env:SONAR_PR_NUMBER

if ([string]::IsNullOrWhiteSpace($projectKey)) {
    throw 'SONAR_PROJECT_KEY is required.'
}
if ([string]::IsNullOrWhiteSpace($sonarToken)) {
    throw 'SONAR_TOKEN is required.'
}
if (-not (Test-Path -LiteralPath $ReportTaskPath -PathType Leaf)) {
    throw 'The scanner metadata file was not created.'
}

$taskLine = Get-Content -LiteralPath $ReportTaskPath |
    Where-Object { $_.StartsWith('ceTaskId=', [StringComparison]::Ordinal) } |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($taskLine)) {
    throw 'The scanner metadata file does not contain a computation task ID.'
}

$taskId = $taskLine.Substring('ceTaskId='.Length)
$headers = @{ Authorization = "Bearer $sonarToken" }
$encodedTaskId = [Uri]::EscapeDataString($taskId)
$deadline = [DateTimeOffset]::UtcNow.AddMinutes(5)

while ($true) {
    $taskUri = "$sonarBaseUrl/api/ce/task?id=$encodedTaskId"
    $taskResponse = Invoke-RestMethod -Uri $taskUri -Headers $headers -Method Get -TimeoutSec 30
    $taskStatus = [string]$taskResponse.task.status
    if ($taskStatus -eq 'SUCCESS') {
        break
    }
    if ($taskStatus -eq 'FAILED' -or $taskStatus -eq 'CANCELED') {
        throw "SonarQube analysis ended with status $taskStatus."
    }
    if ($taskStatus -ne 'PENDING' -and $taskStatus -ne 'IN_PROGRESS') {
        throw "SonarQube returned an unknown task status: $taskStatus."
    }
    if ([DateTimeOffset]::UtcNow -ge $deadline) {
        throw 'SonarQube analysis did not finish within five minutes.'
    }
    Start-Sleep -Seconds 5
}

$analysisId = [string]$taskResponse.task.analysisId
if ([string]::IsNullOrWhiteSpace($analysisId)) {
    throw 'The completed SonarQube task does not have an analysis ID.'
}

function Get-SonarItems {
    param(
        [string]$Endpoint,
        [string]$ItemName,
        [string]$Query
    )

    $items = [System.Collections.Generic.List[object]]::new()
    $page = 1
    do {
        $uri = "$sonarBaseUrl/api/${Endpoint}?${Query}&ps=500&p=$page"
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -TimeoutSec 30
        if ($null -eq $response.paging -or $null -eq $response.$ItemName) {
            throw "SonarQube returned an incomplete response for $Endpoint."
        }
        $pageItems = @($response.$ItemName)
        foreach ($item in $pageItems) {
            $items.Add($item)
        }
        $total = [int]$response.paging.total
        if ($pageItems.Count -eq 0 -and $items.Count -lt $total) {
            throw "SonarQube stopped returning results for $Endpoint."
        }
        $page++
    } while ($items.Count -lt $total)

    return ,$items.ToArray()
}

$encodedProjectKey = [Uri]::EscapeDataString($projectKey)
if ([string]::IsNullOrWhiteSpace($pullRequestNumber)) {
    $scope = 'main'
    $scopeQuery = 'branch=main'
}
else {
    $scope = "pull_request_$pullRequestNumber"
    $encodedPullRequest = [Uri]::EscapeDataString($pullRequestNumber)
    $scopeQuery = "pullRequest=$encodedPullRequest"
}

$issuesQuery = "componentKeys=$encodedProjectKey&resolved=false&$scopeQuery"
$hotspotsQuery = "projectKey=$encodedProjectKey&$scopeQuery"
$issues = Get-SonarItems -Endpoint 'issues/search' -ItemName 'issues' -Query $issuesQuery
$hotspots = Get-SonarItems -Endpoint 'hotspots/search' -ItemName 'hotspots' -Query $hotspotsQuery
$encodedAnalysisId = [Uri]::EscapeDataString($analysisId)
$qualityGateUri = "$sonarBaseUrl/api/qualitygates/project_status?analysisId=$encodedAnalysisId"
$qualityGateResponse = Invoke-RestMethod -Uri $qualityGateUri -Headers $headers -Method Get -TimeoutSec 30

$report = [ordered]@{
    projectKey = $projectKey
    scope = $scope
    sourceCommit = $env:SONAR_HEAD_SHA
    analyzedCheckoutCommit = $env:GITHUB_SHA
    analysisId = $analysisId
    computationTaskId = $taskId
    qualityGate = $qualityGateResponse.projectStatus.status
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    issueCount = $issues.Count
    securityHotspotCount = $hotspots.Count
    issues = $issues
    securityHotspots = $hotspots
}

$report | ConvertTo-Json -Depth 30 |
    Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Exported $($issues.Count) issues and $($hotspots.Count) security hotspots."
