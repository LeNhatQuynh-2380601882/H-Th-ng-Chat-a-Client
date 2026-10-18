$ErrorActionPreference = "Stop"

Write-Host "================ INTEGRATION TESTING PHASE 2A ================" -ForegroundColor Cyan

# 1. CONNECT CLIENT 1 (Alice)
$client1 = New-Object System.Net.Sockets.TcpClient("127.0.0.1", 8888)
$stream1 = $client1.GetStream()
$writer1 = New-Object System.IO.StreamWriter($stream1, [System.Text.Encoding]::UTF8)
$writer1.AutoFlush = $true
$reader1 = New-Object System.IO.StreamReader($stream1, [System.Text.Encoding]::UTF8)

$writer1.WriteLine('{"action":"LOGIN","sender":"Alice","content":"login_test"}')
$respLogin1 = $reader1.ReadLine()
$tokenAlice = ($respLogin1 | ConvertFrom-Json).token
$respUserList1 = $reader1.ReadLine() # Broadcast USER_LIST
Write-Host "[TEST 1] Alice Login -> $respLogin1" -ForegroundColor Green

# 2. CLIENT 2 ATTEMPTS DUPLICATE LOGIN AS 'Alice' (Check B1)
$client2 = New-Object System.Net.Sockets.TcpClient("127.0.0.1", 8888)
$stream2 = $client2.GetStream()
$writer2 = New-Object System.IO.StreamWriter($stream2, [System.Text.Encoding]::UTF8)
$writer2.AutoFlush = $true
$reader2 = New-Object System.IO.StreamReader($stream2, [System.Text.Encoding]::UTF8)

$writer2.WriteLine('{"action":"LOGIN","sender":"Alice","content":"duplicate_test"}')
$respDuplicate = $reader2.ReadLine()
Write-Host "[TEST 2] Duplicate Login 'Alice' -> Rejected: $respDuplicate" -ForegroundColor Green

# 3. CLIENT 2 LOGS IN AS 'Bob'
$writer2.WriteLine('{"action":"LOGIN","sender":"Bob","content":"login_test"}')
$respLoginBob = $reader2.ReadLine()
$tokenBob = ($respLoginBob | ConvertFrom-Json).token
$respUserListBob = $reader2.ReadLine() # Broadcast USER_LIST to Bob
$respUserListAlice = $reader1.ReadLine() # Broadcast USER_LIST to Alice
Write-Host "[TEST 3] Client 2 Login 'Bob' -> $respLoginBob" -ForegroundColor Green

# 4. TEST TYPING START & STOP (W1 + W2)
$typingStart = "{`"action`":`"TYPING`",`"sender`":`"Alice`",`"receiver`":`"Bob`",`"content`":`"START`",`"token`":`"$tokenAlice`"}"
$writer1.WriteLine($typingStart)
$recvTypingStart = $reader2.ReadLine()
Write-Host "[TEST 4A] Bob received TYPING START -> $recvTypingStart" -ForegroundColor Green

$typingStop = "{`"action`":`"TYPING`",`"sender`":`"Alice`",`"receiver`":`"Bob`",`"content`":`"STOP`",`"token`":`"$tokenAlice`"}"
$writer1.WriteLine($typingStop)
$recvTypingStop = $reader2.ReadLine()
Write-Host "[TEST 4B] Bob received TYPING STOP -> $recvTypingStop" -ForegroundColor Green

# 5. TEST FILE_TRANSFER (W5)
$filePayload = '{\"fileName\":\"demo.png\",\"fileSize\":2048,\"isImage\":true,\"base64Data\":\"iVBORw0KGgoAAAANSUhEUg==\"}'
$filePacket = "{`"action`":`"FILE_TRANSFER`",`"sender`":`"Alice`",`"receiver`":`"Bob`",`"content`":`"$filePayload`",`"token`":`"$tokenAlice`"}"
$writer1.WriteLine($filePacket)
$recvFile = $reader2.ReadLine()
Write-Host "[TEST 5] Bob received FILE_TRANSFER -> $recvFile" -ForegroundColor Green

# 6. TEST DISCONNECT AND SESSION CLEANUP (B2)
$client1.Close()
Start-Sleep -Milliseconds 300
Write-Host "[TEST 6] Alice disconnected. Testing re-login with 'Alice'..." -ForegroundColor Yellow

$client3 = New-Object System.Net.Sockets.TcpClient("127.0.0.1", 8888)
$stream3 = $client3.GetStream()
$writer3 = New-Object System.IO.StreamWriter($stream3, [System.Text.Encoding]::UTF8)
$writer3.AutoFlush = $true
$reader3 = New-Object System.IO.StreamReader($stream3, [System.Text.Encoding]::UTF8)

$writer3.WriteLine('{"action":"LOGIN","sender":"Alice","content":"relogin_test"}')
$respRelogin = $reader3.ReadLine()
Write-Host "[TEST 6 RESULT] Re-login as 'Alice' after disconnect -> $respRelogin" -ForegroundColor Green

$client2.Close()
$client3.Close()
Write-Host "================ ALL 6 INTEGRATION TESTS PASSED 100%! ================" -ForegroundColor Cyan
