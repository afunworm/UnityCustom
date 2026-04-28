Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ---- CONFIG ----
$gamePath = "C:\Program Files (x86)\Steam\steamapps\common\YAPYAP"
$cscPath   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
# ----------------

# Form
$form = New-Object System.Windows.Forms.Form
$form.Text = "BepInEx Plugin Compiler"
$form.Size = New-Object System.Drawing.Size(820, 640)
$form.StartPosition = "CenterScreen"
$form.BackColor = [System.Drawing.Color]::FromArgb(30, 30, 30)
$form.ForeColor = [System.Drawing.Color]::White

$pathLabel = New-Object System.Windows.Forms.Label
$pathLabel.Text = "Game Path:"
$pathLabel.Location = New-Object System.Drawing.Point(10, 12)
$pathLabel.Size = New-Object System.Drawing.Size(80, 20)
$pathLabel.ForeColor = [System.Drawing.Color]::LightGray

$pathBox = New-Object System.Windows.Forms.TextBox
$pathBox.Text = $gamePath
$pathBox.Location = New-Object System.Drawing.Point(95, 10)
$pathBox.Size = New-Object System.Drawing.Size(580, 22)
$pathBox.BackColor = [System.Drawing.Color]::FromArgb(50, 50, 50)
$pathBox.ForeColor = [System.Drawing.Color]::White

$browseButton = New-Object System.Windows.Forms.Button
$browseButton.Text = "Browse"
$browseButton.Location = New-Object System.Drawing.Point(685, 8)
$browseButton.Size = New-Object System.Drawing.Size(100, 26)
$browseButton.BackColor = [System.Drawing.Color]::FromArgb(60, 60, 60)
$browseButton.ForeColor = [System.Drawing.Color]::White
$browseButton.FlatStyle = "Flat"
$browseButton.Add_Click({
    $browser = New-Object System.Windows.Forms.FolderBrowserDialog
    $browser.Description = "Select your YAPYAP game folder"
    if ($browser.ShowDialog() -eq "OK") { $pathBox.Text = $browser.SelectedPath }
})

$browseCsButton = New-Object System.Windows.Forms.Button
$browseCsButton.Text = "Open .cs File"
$browseCsButton.Location = New-Object System.Drawing.Point(10, 45)
$browseCsButton.Size = New-Object System.Drawing.Size(100, 20)
$browseCsButton.BackColor = [System.Drawing.Color]::FromArgb(60, 60, 60)
$browseCsButton.ForeColor = [System.Drawing.Color]::White
$browseCsButton.FlatStyle = "Flat"
$browseCsButton.Add_Click({
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "C# files (*.cs)|*.cs"
    $ofd.Title = "Select plugin .cs file"
    if ($ofd.ShowDialog() -eq "OK") {
        $textArea.Text = [System.IO.File]::ReadAllText($ofd.FileName)
    }
})

$codeLabel = New-Object System.Windows.Forms.Label
$codeLabel.Text = "Paste your C# plugin code below:"
$codeLabel.Location = New-Object System.Drawing.Point(120, 48)
$codeLabel.Size = New-Object System.Drawing.Size(300, 20)
$codeLabel.ForeColor = [System.Drawing.Color]::LightGray

$textArea = New-Object System.Windows.Forms.TextBox
$textArea.Multiline = $true
$textArea.ScrollBars = "Vertical"
$textArea.Size = New-Object System.Drawing.Size(780, 460)
$textArea.Location = New-Object System.Drawing.Point(10, 68)
$textArea.BackColor = [System.Drawing.Color]::FromArgb(20, 20, 20)
$textArea.ForeColor = [System.Drawing.Color]::LightGreen
$textArea.Font = New-Object System.Drawing.Font("Consolas", 10)
$textArea.Text = ""

$nameLabel = New-Object System.Windows.Forms.Label
$nameLabel.Text = "Output filename:"
$nameLabel.Location = New-Object System.Drawing.Point(10, 540)
$nameLabel.Size = New-Object System.Drawing.Size(110, 22)
$nameLabel.ForeColor = [System.Drawing.Color]::LightGray

$nameBox = New-Object System.Windows.Forms.TextBox
$nameBox.Text = "HuysHUD"
$nameBox.Location = New-Object System.Drawing.Point(125, 538)
$nameBox.Size = New-Object System.Drawing.Size(200, 22)
$nameBox.BackColor = [System.Drawing.Color]::FromArgb(50, 50, 50)
$nameBox.ForeColor = [System.Drawing.Color]::White

$compileButton = New-Object System.Windows.Forms.Button
$compileButton.Text = "Compile DLL"
$compileButton.Location = New-Object System.Drawing.Point(340, 534)
$compileButton.Size = New-Object System.Drawing.Size(120, 30)
$compileButton.BackColor = [System.Drawing.Color]::FromArgb(0, 120, 60)
$compileButton.ForeColor = [System.Drawing.Color]::White
$compileButton.FlatStyle = "Flat"

$clearButton = New-Object System.Windows.Forms.Button
$clearButton.Text = "Clear"
$clearButton.Location = New-Object System.Drawing.Point(470, 534)
$clearButton.Size = New-Object System.Drawing.Size(80, 30)
$clearButton.BackColor = [System.Drawing.Color]::FromArgb(120, 40, 40)
$clearButton.ForeColor = [System.Drawing.Color]::White
$clearButton.FlatStyle = "Flat"
$clearButton.Add_Click({ $textArea.Text = "" })

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Location = New-Object System.Drawing.Point(560, 540)
$statusLabel.Size = New-Object System.Drawing.Size(240, 22)
$statusLabel.ForeColor = [System.Drawing.Color]::LightGray

$compileButton.Add_Click({
    $currentGamePath = $pathBox.Text
    $outputName = $nameBox.Text
    if (-not $outputName.EndsWith(".dll")) { $outputName += ".dll" }
    $outputPath = "$currentGamePath\BepInEx\plugins\$outputName"
    $managed = "$currentGamePath\yapyap_Data\Managed"

    $tempCs = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.cs'
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($tempCs, $textArea.Text, $utf8NoBom)

    $refs = @(
        "$currentGamePath\BepInEx\core\BepInEx.dll",
        "$currentGamePath\BepInEx\core\0Harmony.dll",
        "$managed\netstandard.dll",
        "$managed\UnityEngine.CoreModule.dll",
        "$managed\UnityEngine.dll",
        "$managed\UnityEngine.IMGUIModule.dll",
        "$managed\UnityEngine.TextRenderingModule.dll",
        "$managed\UnityEngine.InputLegacyModule.dll",
        "$managed\Mirror.dll"
    )

    $missingRefs = $refs | Where-Object { -not (Test-Path $_) }
    if ($missingRefs.Count -gt 0) {
        $statusLabel.Text = "Missing DLLs!"
        $statusLabel.ForeColor = [System.Drawing.Color]::OrangeRed
        [System.Windows.Forms.MessageBox]::Show("Could not find:`n" + ($missingRefs -join "`n"), "Missing References")
        return
    }

    $refArgs = ($refs | ForEach-Object { "/r:`"$_`"" }) -join " "
    $cscArgs = "/target:library /out:`"$outputPath`" $refArgs `"$tempCs`""

    $statusLabel.Text = "Compiling..."
    $statusLabel.ForeColor = [System.Drawing.Color]::Yellow
    $form.Refresh()

    $result = Start-Process -FilePath $cscPath -ArgumentList $cscArgs -Wait -PassThru -NoNewWindow -RedirectStandardOutput "$env:TEMP\csc_out.txt" -RedirectStandardError "$env:TEMP\csc_err.txt"
    Remove-Item $tempCs -ErrorAction SilentlyContinue

    if ($result.ExitCode -eq 0) {
        $statusLabel.Text = "Done! Saved to plugins folder."
        $statusLabel.ForeColor = [System.Drawing.Color]::LightGreen
    } else {
        $errText = Get-Content "$env:TEMP\csc_out.txt" -Raw
        $statusLabel.Text = "Compile error!"
        $statusLabel.ForeColor = [System.Drawing.Color]::OrangeRed
        [System.Windows.Forms.MessageBox]::Show($errText, "Compile Error")
    }
})

$form.Controls.AddRange(@($pathLabel, $pathBox, $browseButton, $browseCsButton, $codeLabel, $textArea, $nameLabel, $nameBox, $compileButton, $clearButton, $statusLabel))
[void]$form.ShowDialog()