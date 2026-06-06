[CmdletBinding(SupportsShouldProcess)]
param(
	[Parameter(Mandatory, Position = 0)]
	[string] $WorkshopRoot,

	[switch] $AllowMissing
)

$ErrorActionPreference = 'Stop'

function Test-AbsolutePath {
	param(
		[Parameter(Mandatory)]
		[string] $Path
	)

	return $Path -match '^[A-Za-z]:[\\/]' -or $Path -match '^\\\\[^\\/]+[\\/][^\\/]+'
}

function Remove-ReferencePath {
	param(
		[Parameter(Mandatory)]
		[System.IO.FileSystemInfo] $Item
	)

	if ($Item.PSIsContainer) {
		if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
			[System.IO.Directory]::Delete($Item.FullName)
			return
		}

		foreach ($child in Get-ChildItem -LiteralPath $Item.FullName -Force) {
			Remove-ReferencePath -Item $child
		}

		[System.IO.Directory]::Delete($Item.FullName)
		return
	}

	Remove-Item -LiteralPath $Item.FullName -Force
}

function Initialize-ReferenceDirectory {
	[CmdletBinding(SupportsShouldProcess)]
	param(
		[Parameter(Mandatory)]
		[string] $Path
	)

	$item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
	if ($null -eq $item) {
		if ($PSCmdlet.ShouldProcess($Path, 'Create reference directory')) {
			New-Item -ItemType Directory -Path $Path | Out-Null
		}

		return
	}

	if ($item.PSIsContainer -and (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0)) {
		return
	}

	if ($PSCmdlet.ShouldProcess($Path, 'Replace reference path with directory')) {
		Remove-ReferencePath -Item $item
		New-Item -ItemType Directory -Path $Path | Out-Null
	}
}

function Get-ReferenceHash {
	param(
		[Parameter(Mandatory)]
		[string] $Path
	)

	$stream = [System.IO.File]::OpenRead($Path)
	try {
		$sha = [System.Security.Cryptography.SHA256]::Create()
		try {
			return [System.BitConverter]::ToString($sha.ComputeHash($stream))
		}
		finally {
			$sha.Dispose()
		}
	}
	finally {
		$stream.Dispose()
	}
}

function Copy-ReferenceAssembly {
	[CmdletBinding(SupportsShouldProcess)]
	param(
		[Parameter(Mandatory)]
		[System.IO.FileInfo] $Source,

		[Parameter(Mandatory)]
		[string] $Destination
	)

	$item = Get-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
	if ($null -ne $item) {
		if ($item.PSIsContainer -or (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
			if ($PSCmdlet.ShouldProcess($Destination, 'Replace existing reference path with file copy')) {
				Remove-ReferencePath -Item $item
				Copy-Item -LiteralPath $Source.FullName -Destination $Destination
				$script:copied++
			}

			return
		}

		if ((Get-ReferenceHash -Path $item.FullName) -eq (Get-ReferenceHash -Path $Source.FullName)) {
			$script:unchanged++
			return
		}
	}

	if ($PSCmdlet.ShouldProcess($Destination, "Copy from $($Source.FullName)")) {
		Copy-Item -LiteralPath $Source.FullName -Destination $Destination -Force
		$script:copied++
	}
}

function Sync-ReferenceDirectory {
	[CmdletBinding(SupportsShouldProcess)]
	param(
		[Parameter(Mandatory)]
		[string] $Path,

		[Parameter(Mandatory)]
		[System.IO.FileInfo[]] $Assemblies
	)

	Initialize-ReferenceDirectory -Path $Path
	if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
		return
	}

	$expected = @{}
	foreach ($assembly in $Assemblies) {
		$expected[$assembly.Name] = $true
	}

	foreach ($item in Get-ChildItem -LiteralPath $Path -Force) {
		if ($expected.ContainsKey($item.Name)) {
			continue
		}

		if ($PSCmdlet.ShouldProcess($item.FullName, 'Remove stale reference output')) {
			Remove-ReferencePath -Item $item
			$script:removed++
		}
	}

	foreach ($assembly in $Assemblies) {
		Copy-ReferenceAssembly -Source $assembly -Destination (Join-Path $Path $assembly.Name)
	}
}

if (-not (Test-AbsolutePath -Path $WorkshopRoot)) {
	throw "WorkshopRoot must be an absolute path to RimWorld's Steam workshop root."
}

$workshopRootPath = (Resolve-Path -LiteralPath $WorkshopRoot).ProviderPath
$refsRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\References'))
$indexPath = Join-Path $refsRoot 'Index.json'

if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
	throw "Reference index not found: $indexPath"
}

$index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
$entries = @($index.PSObject.Properties | Sort-Object Name)
$assembliesByName = [ordered] @{}
$blockedNames = @{}
$missing = [System.Collections.Generic.List[string]]::new()
$script:copied = 0
$script:removed = 0
$script:unchanged = 0

foreach ($prop in $entries) {
	$name = $prop.Name
	$def = $prop.Value
	$modRoot = Join-Path $workshopRootPath ([string] $def.Id)
	$assemblies = @()
	$seen = @{}

	if (-not (Test-Path -LiteralPath $modRoot -PathType Container)) {
		$missing.Add("${name}: missing workshop item $($def.Id)")
		$blockedNames[$name] = $true
		$assembliesByName[$name] = $assemblies
		continue
	}

	foreach ($pattern in @($def.Assemblies)) {
		$glob = Join-Path $modRoot ($pattern -replace '/', [System.IO.Path]::DirectorySeparatorChar)
		$results = @(Get-ChildItem -Path $glob -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -ieq '.dll' })

		if ($results.Count -eq 0) {
			$missing.Add("${name}: no assemblies matched $pattern")
			$blockedNames[$name] = $true
			continue
		}

		foreach ($assembly in $results) {
			if ($seen.ContainsKey($assembly.Name)) {
				throw "Duplicate reference output path: $(Join-Path (Join-Path $refsRoot $name) $assembly.Name)"
			}

			$seen[$assembly.Name] = $true
			$assemblies += $assembly
		}
	}

	$assembliesByName[$name] = $assemblies
}

if ($missing.Count -gt 0 -and -not $AllowMissing) {
	throw "Reference build blocked by missing inputs:`n- $($missing -join "`n- ")"
}

foreach ($prop in $entries) {
	$name = $prop.Name
	if ($AllowMissing -and $blockedNames.ContainsKey($name)) {
		continue
	}

	Sync-ReferenceDirectory -Path (Join-Path $refsRoot $name) -Assemblies $assembliesByName[$name]
}

if ($missing.Count -gt 0) {
	Write-Warning "Reference build skipped missing inputs and kept existing committed copies:`n- $($missing -join "`n- ")"
}

Write-Host "Reference build complete: copied $copied, unchanged $unchanged, removed stale $removed."
