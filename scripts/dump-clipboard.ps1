param([string]$OutDir = "D:\copy-to-md\diagnostics")

Add-Type -AssemblyName System.Windows.Forms
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$data = [System.Windows.Forms.Clipboard]::GetDataObject()
if ($null -eq $data) {
    Write-Host "clipboard empty"
    exit 1
}

$formats = $data.GetFormats()
$formats | Out-File -FilePath (Join-Path $OutDir "formats.txt") -Encoding utf8
Write-Host ("formats: " + ($formats -join ", "))

function Save-Format([string]$name, [string]$file) {
    if ($data.GetDataPresent($name)) {
        $value = $data.GetData($name)
        if ($value -is [string]) {
            [System.IO.File]::WriteAllText((Join-Path $OutDir $file), $value, [System.Text.Encoding]::UTF8)
            Write-Host ("saved " + $file + " (" + $value.Length + " chars)")
        } elseif ($value -is [System.IO.MemoryStream]) {
            $bytes = $value.ToArray()
            [System.IO.File]::WriteAllBytes((Join-Path $OutDir $file), $bytes)
            Write-Host ("saved " + $file + " (" + $bytes.Length + " bytes)")
        } else {
            Write-Host ($name + " type: " + $value.GetType().FullName)
        }
    }
}

Save-Format "UnicodeText" "unicode.txt"
Save-Format "Text" "text.txt"
Save-Format "CF_HTML" "html.txt"
Save-Format "HTML Format" "html2.txt"
Save-Format "Rich Text Format" "rtf.txt"