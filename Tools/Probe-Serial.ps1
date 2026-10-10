$ErrorActionPreference = 'Continue'

function Get-Crc16([byte[]]$data) {
    $crc = 0xFFFF
    foreach ($b in $data) {
        $crc = $crc -bxor $b
        for ($i = 0; $i -lt 8; $i++) {
            if ($crc -band 1) { $crc = (($crc -shr 1) -bxor 0xA001) -band 0xFFFF }
            else { $crc = ($crc -shr 1) -band 0xFFFF }
        }
    }
    return $crc
}

$payload = @(0x01, 0x03, 0x01, 0x00, 0x00, 0x0A) | ForEach-Object { [byte]$_ }
$crc = Get-Crc16 $payload
$frame = @()
foreach ($b in $payload) { $frame += [byte]$b }
$frame += [byte]($crc -band 0xFF)
$frame += [byte](($crc -shr 8) -band 0xFF)

Write-Host "Request: $(($frame | ForEach-Object { $_.ToString('X2') }) -join ' ')"
Write-Host "Probing..."

$ports = [System.IO.Ports.SerialPort]::GetPortNames()
Write-Host "Found ports: $($ports -join ', ')"

foreach ($portName in $ports) {
    try {
        $port = New-Object System.IO.Ports.SerialPort($portName, 38400, 'None', 8, 'One')
        $port.ReadTimeout = 900
        $port.WriteTimeout = 400
        $port.Open()
        $port.DiscardInBuffer()
        $port.Write($frame, 0, $frame.Length)
        $buf = New-Object byte[] 32
        $total = 0
        $deadline = [DateTime]::Now.AddMilliseconds(1000)
        while ($total -lt 25 -and [DateTime]::Now -lt $deadline) {
            try {
                $n = $port.Read($buf, $total, 32 - $total)
                $total += $n
            } catch {
                break
            }
        }
        if ($total -gt 0) {
            $resp = ($buf[0..([Math]::Min($total, 24))] | ForEach-Object { $_.ToString('X2') }) -join ' '
            Write-Host "PORT $portName -> RESPONSE ($total B): $resp"
        } else {
            Write-Host "PORT $portName -> NO RESPONSE (timeout)"
        }
        $port.Close()
    } catch {
        Write-Host "PORT $portName -> FAILED: $($_.Exception.Message)"
    }
}
Write-Host '---done---'
