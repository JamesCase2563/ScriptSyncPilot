$ErrorActionPreference = 'Stop'

$script:BaseUri = 'https://www.handyfeeling.com/api/handy-rest/v3/'
$script:ApplicationId = 'LGtuDB6Ikqu4s65pK7eoshlqkDV_ziiG'
$script:Headers = $null
$script:OriginalOffset = $null
$script:OriginalStroke = $null
$script:HasWritten = $false

function Get-ResponseBody {
    param($Response)

    if ($null -eq $Response) { return '' }

    if ($Response.PSObject.Properties['Content']) {
        $content = $Response.Content
        if ($content -is [string]) { return $content }
        if ($null -ne $content -and $content.PSObject.Methods['ReadAsStringAsync']) {
            return $content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }

    if ($Response.PSObject.Methods['GetResponseStream']) {
        $stream = $Response.GetResponseStream()
        if ($null -eq $stream) { return '' }
        $reader = New-Object System.IO.StreamReader($stream)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }

    return ''
}

function Invoke-DeviceApi {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body
    )

    $parameters = @{
        Uri             = $script:BaseUri + $Path
        Method          = $Method
        Headers         = $script:Headers
        UseBasicParsing = $true
        TimeoutSec      = 15
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ConvertTo-Json -InputObject $Body -Compress -Depth 8
    }

    $statusCode = 0
    $raw = ''
    try {
        $response = Invoke-WebRequest @parameters
        $statusCode = [int]$response.StatusCode
        $raw = Get-ResponseBody $response
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) {
            throw "Request failed for $Method ${Path}: $($_.Exception.Message)"
        }
        if ($response.PSObject.Properties['StatusCode']) {
            $statusCode = [int]$response.StatusCode
        }
        $raw = Get-ResponseBody $response
    }

    $payload = $null
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        try { $payload = ConvertFrom-Json -InputObject $raw }
        catch { $payload = $raw }
    }

    $deviceError = $null
    if ($null -ne $payload -and $payload -isnot [string] -and $payload.PSObject.Properties['error']) {
        $deviceError = $payload.error
    }

    return [pscustomobject]@{
        Method        = $Method
        Path          = $Path
        StatusCode    = $statusCode
        Payload       = $payload
        DeviceError   = $deviceError
        HttpSucceeded = ($statusCode -ge 200 -and $statusCode -lt 300)
    }
}

function Get-Result {
    param($Call)

    if ($null -eq $Call.Payload -or $Call.Payload -is [string]) { return $null }
    if ($Call.Payload.PSObject.Properties['result']) { return $Call.Payload.result }
    return $null
}

function Show-Call {
    param($Call)

    Write-Host ("{0} {1} -> HTTP {2}" -f $Call.Method, $Call.Path, $Call.StatusCode)
    if ($null -ne $Call.DeviceError) {
        Write-Host ("  DEVICE ERROR: code={0}; name={1}; message={2}" -f $Call.DeviceError.code, $Call.DeviceError.name, $Call.DeviceError.message) -ForegroundColor Yellow
    }
    elseif ($null -ne $Call.Payload) {
        $display = $Call.Payload
        if ($Call.Payload -isnot [string] -and $Call.Payload.PSObject.Properties['result']) {
            $display = $Call.Payload.result
        }
        ConvertTo-Json -InputObject $display -Depth 8
    }

    if (-not $Call.HttpSucceeded) {
        Write-Host '  HTTP request failed; inspect the status and response above.' -ForegroundColor Yellow
    }
}

function Get-OffsetValue {
    param($Call)
    $result = Get-Result $Call
    if ($null -ne $result -and $null -ne $result.offset) { return [long]$result.offset }
    return $null
}

function Get-StrokeValue {
    param($Call)
    $result = Get-Result $Call
    if ($null -ne $result -and $null -ne $result.min -and $null -ne $result.max) {
        return [pscustomobject]@{ min = [double]$result.min; max = [double]$result.max }
    }
    return $null
}

function Read-Snapshot {
    $calls = @{}
    foreach ($path in @('info', 'hstp/offset', 'slider/stroke', 'settings/slider', 'capabilities', 'slider/state')) {
        try {
            $call = Invoke-DeviceApi -Method GET -Path $path
            $calls[$path] = $call
            Show-Call $call
        }
        catch {
            Write-Host ("GET {0} failed: {1}" -f $path, $_.Exception.Message) -ForegroundColor Yellow
        }
    }

    $offset = $null
    if ($calls.ContainsKey('hstp/offset')) { $offset = Get-OffsetValue $calls['hstp/offset'] }
    $stroke = $null
    if ($calls.ContainsKey('slider/stroke')) { $stroke = Get-StrokeValue $calls['slider/stroke'] }

    $capturedBaseline = $false
    if ($null -ne $offset -and $null -eq $script:OriginalOffset) {
        $script:OriginalOffset = $offset
        $capturedBaseline = $true
    }
    if ($null -ne $stroke -and $null -eq $script:OriginalStroke) {
        $script:OriginalStroke = $stroke
        $capturedBaseline = $true
    }
    if ($capturedBaseline) {
        Write-Host 'Restoration values captured in memory for this run only.' -ForegroundColor Green
    }
    elseif ($null -ne $script:OriginalOffset -or $null -ne $script:OriginalStroke) {
        Write-Host 'The original restoration values are unchanged.' -ForegroundColor Green
    }
}

function Set-Offset {
    $text = Read-Host 'Offset in milliseconds (writes immediately)'
    [long]$offset = 0
    if (-not [long]::TryParse($text, [ref]$offset)) {
        Write-Host 'Enter a whole number of milliseconds.' -ForegroundColor Yellow
        return
    }

    $call = Invoke-DeviceApi -Method PUT -Path 'hstp/offset' -Body @{ offset = $offset }
    Show-Call $call
    $script:HasWritten = $true
}

function Set-Stroke {
    $minText = Read-Host 'Minimum stroke percent (0-100; writes immediately)'
    $maxText = Read-Host 'Maximum stroke percent (0-100; writes immediately)'
    [double]$minimum = 0
    [double]$maximum = 0
    $style = [System.Globalization.NumberStyles]::Float
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    if (-not [double]::TryParse($minText, $style, $culture, [ref]$minimum) -or
        -not [double]::TryParse($maxText, $style, $culture, [ref]$maximum) -or
        $minimum -lt 0 -or $maximum -gt 100 -or $minimum -ge $maximum) {
        Write-Host 'Use numbers with a dot as decimal separator, with 0 <= min < max <= 100.' -ForegroundColor Yellow
        return
    }

    $call = Invoke-DeviceApi -Method PUT -Path 'slider/stroke' -Body @{
        min = $minimum / 100.0
        max = $maximum / 100.0
    }
    Show-Call $call
    $script:HasWritten = $true
}

function Watch-DeviceValues {
    $secondsText = Read-Host 'Watch duration in seconds (polls every 5 seconds)'
    [int]$seconds = 0
    if (-not [int]::TryParse($secondsText, [ref]$seconds) -or $seconds -lt 5) {
        Write-Host 'Enter a duration of at least 5 seconds.' -ForegroundColor Yellow
        return
    }

    $lastOffset = $null
    $lastStroke = $null
    $offsetCall = Invoke-DeviceApi -Method GET -Path 'hstp/offset'
    $strokeCall = Invoke-DeviceApi -Method GET -Path 'slider/stroke'
    $lastOffset = Get-OffsetValue $offsetCall
    $lastStroke = Get-StrokeValue $strokeCall
    Show-Call $offsetCall
    Show-Call $strokeCall

    $end = [DateTime]::UtcNow.AddSeconds($seconds)
    while ([DateTime]::UtcNow -lt $end) {
        $remaining = [int][Math]::Ceiling(($end - [DateTime]::UtcNow).TotalSeconds)
        Start-Sleep -Seconds ([Math]::Min(5, [Math]::Max(1, $remaining)))

        $offsetCall = Invoke-DeviceApi -Method GET -Path 'hstp/offset'
        $strokeCall = Invoke-DeviceApi -Method GET -Path 'slider/stroke'
        $offset = Get-OffsetValue $offsetCall
        $stroke = Get-StrokeValue $strokeCall
        $time = [DateTime]::Now.ToString('HH:mm:ss')

        if ($null -ne $offset -and $offset -ne $lastOffset) {
            Write-Host ("[{0}] Offset changed: {1} -> {2} ms" -f $time, $lastOffset, $offset) -ForegroundColor Cyan
            $lastOffset = $offset
        }
        if ($null -ne $stroke -and ($null -eq $lastStroke -or $stroke.min -ne $lastStroke.min -or $stroke.max -ne $lastStroke.max)) {
            Write-Host ("[{0}] Stroke changed: {1:P0}-{2:P0} -> {3:P0}-{4:P0}" -f $time, $lastStroke.min, $lastStroke.max, $stroke.min, $stroke.max) -ForegroundColor Cyan
            $lastStroke = $stroke
        }
        if (-not $offsetCall.HttpSucceeded -or $null -ne $offsetCall.DeviceError) { Show-Call $offsetCall }
        if (-not $strokeCall.HttpSucceeded -or $null -ne $strokeCall.DeviceError) { Show-Call $strokeCall }
    }
}

function Restore-OriginalValues {
    if ($null -ne $script:OriginalOffset) {
        $call = Invoke-DeviceApi -Method PUT -Path 'hstp/offset' -Body @{ offset = $script:OriginalOffset }
        Show-Call $call
    }
    if ($null -ne $script:OriginalStroke) {
        $call = Invoke-DeviceApi -Method PUT -Path 'slider/stroke' -Body @{
            min = $script:OriginalStroke.min
            max = $script:OriginalStroke.max
        }
        Show-Call $call
    }
    if ($null -eq $script:OriginalOffset -and $null -eq $script:OriginalStroke) {
        Write-Host 'No original values were captured. Run the snapshot option before changing device values.' -ForegroundColor Yellow
    }
}

Write-Host 'Handy device contract probe' -ForegroundColor Cyan
Write-Host 'Writes change the device immediately. Capture a snapshot first; restore values before exit.' -ForegroundColor Yellow
$secureKey = Read-Host 'Device connection key (input hidden)' -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $connectionKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    $script:Headers = @{
        Accept                 = 'application/json'
        'X-Api-Key'            = $script:ApplicationId
        'X-Connection-Key'     = $connectionKey
    }

    while ($true) {
        Write-Host ''
        Write-Host '1. Read device snapshot (also captures original offset/stroke)'
        Write-Host '2. Set HSTP offset'
        Write-Host '3. Set slider stroke range'
        Write-Host '4. Watch offset/stroke for external changes'
        Write-Host '5. Restore captured offset/stroke'
        Write-Host 'Q. Quit'
        $choice = Read-Host 'Choose'

        switch ($choice.ToUpperInvariant()) {
            '1' { Read-Snapshot }
            '2' { Set-Offset }
            '3' { Set-Stroke }
            '4' { Watch-DeviceValues }
            '5' { Restore-OriginalValues }
            'Q' { break }
            default { Write-Host 'Unknown option.' -ForegroundColor Yellow }
        }

        if ($choice.ToUpperInvariant() -eq 'Q') { break }
    }

    if ($script:HasWritten) {
        $restore = Read-Host 'This run issued writes. Restore the captured original values now? (y/N)'
        if ($restore -match '^(y|yes)$') { Restore-OriginalValues }
    }
}
finally {
    if ($null -ne $script:Headers) { $script:Headers.Remove('X-Connection-Key') }
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    $connectionKey = $null
    $secureKey.Dispose()
}