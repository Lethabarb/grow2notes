# The Grow2Notes setup window (decisions.md D102). Its first button makes a provider's organisation and first manager
# on the operator's own Azure CLI sign-in, so their details reach no AI provider. Its second opens Claude Code in its
# own folder, connected to production's AI-assistant access. Start it with "Grow2Notes setup.cmd".

param([ValidateSet('prod', 'test')][string]$Environment = 'prod')

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$ErrorActionPreference = 'Stop'

$script:Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
# One folder for each environment, because Claude Code keeps a connection for each folder (onboard-provider.md).
$script:AdminFolder = Join-Path $HOME $(if ($Environment -eq 'prod') { 'grow2notes-admin' } else { 'grow2notes-admin-test' })
$script:McpAddress = "https://app-grow2notes-$Environment.azurewebsites.net/mcp"
$script:Utf8 = New-Object System.Text.UTF8Encoding($false)

function Find-Bash {
    $git = (Get-Command git.exe).Source
    Join-Path (Split-Path (Split-Path $git)) 'bin\bash.exe'
}

function Invoke-CreateOrganisation([string[]]$Values) {
    $script = (Join-Path $PSScriptRoot 'create-organisation.sh') -replace '\\', '/'
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = Find-Bash
    $start.Arguments = '"{0}" {1}' -f $script, $Environment
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = $script:Utf8

    $process = [System.Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEndAsync()
    $null = $process.StandardError.ReadToEndAsync()

    # As bytes, because Windows PowerShell would otherwise re-encode an accented name. Line feeds only, as bash reads them.
    $bytes = $script:Utf8.GetBytes(($Values -join "`n") + "`n")
    $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $process.StandardInput.Close()

    while (-not $process.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }

    [pscustomobject]@{ Code = $process.ExitCode; Output = $output.Result.Trim() }
}

function Initialize-AdminFolder {
    $skills = Join-Path $script:AdminFolder '.claude\skills'
    New-Item -ItemType Directory -Force $skills | Out-Null
    Copy-Item -Recurse -Force (Join-Path $script:Repo '.claude\skills\onboard-provider') $skills

    # The permissions block of by-hand.md A3, read from there so that there is one copy of it.
    $byHand = Get-Content -Raw -Encoding UTF8 (Join-Path $skills 'onboard-provider\by-hand.md')
    $block = [regex]::Match($byHand, '(?s)```json\r?\n(\{.*?\})\r?\n```').Groups[1].Value
    if (-not $block) { throw 'The permissions block was not found in by-hand.md.' }
    [System.IO.File]::WriteAllText((Join-Path $script:AdminFolder '.claude\settings.local.json'), $block, $script:Utf8)
}

function Invoke-Claude([string[]]$Arguments) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command claude).Source
    $start.Arguments = $Arguments -join ' '
    $start.WorkingDirectory = $script:AdminFolder
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($start)
    $null = $process.StandardOutput.ReadToEndAsync()
    $null = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $process.ExitCode
}

function Open-ClaudeCode {
    Initialize-AdminFolder

    # Local scope, the default: only a session started in this folder can see the server (by-hand.md C3).
    if ((Invoke-Claude @('mcp', 'get', 'grow2notes')) -ne 0) {
        if ((Invoke-Claude @('mcp', 'add', '--transport', 'http', 'grow2notes', $script:McpAddress)) -ne 0) {
            throw 'Claude Code could not add the Grow2Notes connection.'
        }
    }

    $command = "`$env:CLAUDE_CODE_DISABLE_AUTO_MEMORY = '1'; claude --permission-mode default --add-dir '$($script:Repo)'"
    Start-Process powershell.exe -WorkingDirectory $script:AdminFolder -ArgumentList @('-NoExit', '-NoProfile', '-Command', $command)
}

function New-SetupForm {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = if ($Environment -eq 'prod') { 'Grow2Notes setup' } else { 'Grow2Notes setup (TEST)' }
    $form.ClientSize = New-Object System.Drawing.Size(500, 420)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)

    $create = New-Object System.Windows.Forms.GroupBox
    $create.Text = '1. Create the organisation (once only)'
    $create.SetBounds(12, 10, 476, 262)
    $form.Controls.Add($create)

    $script:Fields = @{}
    $y = 28
    foreach ($row in @(
            @{ Key = 'Organisation'; Label = 'Organisation name, as reports should print it'; Max = 200 },
            @{ Key = 'Name'; Label = "First manager's name"; Max = 100 },
            @{ Key = 'Email'; Label = "First manager's email address"; Max = 256 })) {
        $label = New-Object System.Windows.Forms.Label
        $label.Text = $row.Label
        $label.SetBounds(14, $y, 446, 22)
        $create.Controls.Add($label)
        $box = New-Object System.Windows.Forms.TextBox
        $box.MaxLength = $row.Max
        $box.SetBounds(14, ($y + 23), 446, 26)
        $create.Controls.Add($box)
        $script:Fields[$row.Key] = $box
        $y += 58
    }

    $script:CreateButton = New-Object System.Windows.Forms.Button
    $script:CreateButton.Text = 'Create organisation'
    $script:CreateButton.SetBounds(14, 208, 170, 36)
    $create.Controls.Add($script:CreateButton)

    $connect = New-Object System.Windows.Forms.GroupBox
    $connect.Text = '2. Connect Claude Code (after your Support account is set up)'
    $connect.SetBounds(12, 282, 476, 74)
    $form.Controls.Add($connect)

    $connectButton = New-Object System.Windows.Forms.Button
    $connectButton.Text = 'Open Claude Code for Grow2Notes'
    $connectButton.SetBounds(14, 26, 270, 36)
    $connect.Controls.Add($connectButton)

    $script:Status = New-Object System.Windows.Forms.Label
    $script:Status.SetBounds(14, 362, 474, 52)
    $form.Controls.Add($script:Status)

    $script:CreateButton.Add_Click({
            $values = @($script:Fields.Organisation.Text.Trim(), $script:Fields.Name.Text.Trim(), $script:Fields.Email.Text.Trim())
            if ($values -contains '' -or $values[2] -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
                [void][System.Windows.Forms.MessageBox]::Show('Fill in all three, with a whole email address.', 'Grow2Notes setup')
                return
            }

            $question = "Create this organisation?`n`nOrganisation: $($values[0])`nFirst manager: $($values[1])`nEmail: $($values[2])" +
            "`n`nThis is done once only. The organisation's name cannot be changed afterwards."
            if ([System.Windows.Forms.MessageBox]::Show($question, 'Grow2Notes setup', 'YesNo', 'Question') -ne 'Yes') { return }

            $script:CreateButton.Enabled = $false
            $script:Status.Text = 'Working. This takes about 2 minutes. A browser opens if Azure needs you to sign in.'
            try {
                $result = Invoke-CreateOrganisation $values
                $script:Status.Text = if ($result.Output) { $result.Output } else { "It stopped with code $($result.Code)." }
                if ($result.Code -eq 0) {
                    [void][System.Windows.Forms.MessageBox]::Show(
                        "Done. The first manager's setup email is on its way. Its title is: Set up your Grow2Notes sign-in.",
                        'Grow2Notes setup')
                }
                elseif ($result.Code -eq 4) {
                    # The button stays off: a second try now could make a second organisation (decisions.md D102).
                    [void][System.Windows.Forms.MessageBox]::Show(
                        "$($script:Status.Text)`n`nLook for the manager's email first. Close this window, and wait ten minutes before opening it again.",
                        'Grow2Notes setup', 'OK', 'Warning')
                }
                else {
                    $script:CreateButton.Enabled = $true
                    [void][System.Windows.Forms.MessageBox]::Show($script:Status.Text, 'Grow2Notes setup', 'OK', 'Warning')
                }
            }
            catch {
                $script:CreateButton.Enabled = $true
                $script:Status.Text = "It did not finish: $($_.Exception.Message)"
            }
        })

    $connectButton.Add_Click({
            try {
                Open-ClaudeCode
                $script:Status.Text = 'In the new window, accept the folder prompt, then type /mcp, choose grow2notes and sign in.'
            }
            catch {
                $script:Status.Text = "It did not open: $($_.Exception.Message)"
            }
        })

    $form
}

# Dot-sourcing the script defines its functions and opens nothing, so they can be tried one at a time.
if ($MyInvocation.InvocationName -ne '.') {
    # One window at a time, so that two cannot both pass the check for an existing organisation.
    $mutex = New-Object System.Threading.Mutex($false, 'Local\Grow2NotesSetup')
    if (-not $mutex.WaitOne(0)) {
        [void][System.Windows.Forms.MessageBox]::Show('The setup window is already open.', 'Grow2Notes setup')
        return
    }
    [System.Windows.Forms.Application]::EnableVisualStyles()
    [void](New-SetupForm).ShowDialog()
    $mutex.ReleaseMutex()
}
