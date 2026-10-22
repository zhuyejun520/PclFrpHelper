<#
.SYNOPSIS
    Patches a PCL 2.12.6.2 executable so that the 联机 page shows an FRP entry.

.DESCRIPTION
    1. Un-hides the 联机 title tab and the settings sidebar item.
    2. Bypasses the server-side DisableReason2 kill switch in PageLinkMain.InitConfig.
    3. Embeds PclFrpAddon.dll as an assembly resource.
    4. Injects PageLinkMain.FrpAttachAddon(), which loads that resource with
       Assembly.Load(byte[]) and calls PclFrpAddon.Ui.Attach(PanSelect) via reflection.
    5. Calls FrpAttachAddon() at the end of PageLinkMain's constructor.

    The original executable is never modified: a patched copy is written to -OutputExe.

.PARAMETER InputExe
    Path to an unmodified "Plain Craft Launcher 2.exe" (2.12.6.2, build 391).

.PARAMETER AddonDll
    Path to a compiled PclFrpAddon.dll.

.PARAMETER OutputExe
    Where to write the patched executable.

.PARAMETER CecilPath
    Path to Mono.Cecil.dll (NuGet package Mono.Cecil, lib/netstandard2.0).

.EXAMPLE
    ./patch-pcl.ps1 -InputExe "C:\PCL\Plain Craft Launcher 2.exe" `
                    -AddonDll ".\build\PclFrpAddon.dll" `
                    -OutputExe ".\dist\Plain Craft Launcher 2.exe" `
                    -CecilPath "C:\tools\Mono.Cecil.dll"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputExe,
    [Parameter(Mandatory = $true)][string]$AddonDll,
    [Parameter(Mandatory = $true)][string]$OutputExe,
    [Parameter(Mandatory = $true)][string]$CecilPath,
    [string]$ReferenceAssemblies = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Resolve-Path $CecilPath).Path

$InputExe = (Resolve-Path $InputExe).Path
$AddonDll = (Resolve-Path $AddonDll).Path
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $fw)) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($fw)
$resolver.AddSearchDirectory((Join-Path $fw 'WPF'))
if ($ReferenceAssemblies) { $resolver.AddSearchDirectory($ReferenceAssemblies) }
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $resolver
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($InputExe, $rp)
$mod = $asm.MainModule
$mscorlib = $mod.AssemblyReferences | Where-Object { $_.Name -eq 'mscorlib' } | Select-Object -First 1

$void = $mod.TypeSystem.Void
$str = $mod.TypeSystem.String
$obj = $mod.TypeSystem.Object
$byte = $mod.TypeSystem.Byte
$i4 = $mod.TypeSystem.Int32
$i8 = $mod.TypeSystem.Int64

function New-TypeRef($ns, $name) { New-Object Mono.Cecil.TypeReference($ns, $name, $mod, $mscorlib) }
function New-MethodRef($name, $ret, $decl, $paramTypes, $hasThis) {
    $m = New-Object Mono.Cecil.MethodReference($name, $ret, $decl)
    foreach ($p in $paramTypes) { $m.Parameters.Add((New-Object Mono.Cecil.ParameterDefinition($p))) }
    $m.HasThis = $hasThis
    return $m
}

$tAssembly = New-TypeRef 'System.Reflection' 'Assembly'
$tType = New-TypeRef 'System' 'Type'
$tMethodInfo = New-TypeRef 'System.Reflection' 'MethodInfo'
$tStream = New-TypeRef 'System.IO' 'Stream'
$tBinaryReader = New-TypeRef 'System.IO' 'BinaryReader'
$byteArray = New-Object Mono.Cecil.ArrayType($byte, 1)
$objArray = New-Object Mono.Cecil.ArrayType($obj, 1)

$mGetExecuting = New-MethodRef 'GetExecutingAssembly' $tAssembly $tAssembly @() $false
$mGetResStream = New-MethodRef 'GetManifestResourceStream' $tStream $tAssembly @($str) $true
$mStreamLength = New-MethodRef 'get_Length' $i8 $tStream @() $true
$mBrCtor = New-MethodRef '.ctor' $void $tBinaryReader @($tStream) $true
$mReadBytes = New-MethodRef 'ReadBytes' $byteArray $tBinaryReader @($i4) $true
$mAsmLoadBytes = New-MethodRef 'Load' $tAssembly $tAssembly @($byteArray) $false
$mGetType = New-MethodRef 'GetType' $tType $tAssembly @($str) $true
$mGetMethod = New-MethodRef 'GetMethod' $tMethodInfo $tType @($str) $true
$mInvoke = New-MethodRef 'Invoke' $obj $tMethodInfo @($obj, $objArray) $true

$pl = $mod.Types | Where-Object { $_.FullName -eq 'PCL.PageLinkMain' }
if (-not $pl) { throw 'PCL.PageLinkMain not found - is this PCL 2.12.6.2?' }
$getPanSelect = $pl.Methods | Where-Object { $_.Name -eq 'get_PanSelect' }

# 1) show the 联机 tab and the settings sidebar entry
$psu = $mod.Types | Where-Object { $_.FullName -eq 'PCL.PageSetupUI' }
$hr = $psu.Methods | Where-Object { $_.Name -eq 'HiddenRefresh' }
$ins = $hr.Body.Instructions
$il = $hr.Body.GetILProcessor()
for ($i = 2; $i -lt $ins.Count; $i++) {
    if ($ins[$i].OpCode -eq [Mono.Cecil.Cil.OpCodes]::Ldc_I4_2 -and
        $ins[$i + 1].OpCode -eq [Mono.Cecil.Cil.OpCodes]::Callvirt -and
        $ins[$i + 1].Operand.Name -eq 'set_Visibility' -and
        $ins[$i - 1].Operand.Name -in @('get_BtnTitleSelect2', 'get_ItemLink')) {
        $il.Replace($ins[$i], [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
    }
}

# 2) bypass the server-side DisableReason2 check
$ic = $pl.Methods | Where-Object { $_.Name -eq 'InitConfig' }
$ins2 = $ic.Body.Instructions
$t2 = $null
for ($i = 1; $i -lt $ins2.Count; $i++) {
    if ($ins2[$i].OpCode -eq [Mono.Cecil.Cil.OpCodes]::Brtrue_s -and $ins2[$i - 1].Operand.Name -eq 'IsNullOrEmpty') { $t2 = $ins2[$i]; break }
}
if (-not $t2) { throw 'DisableReason2 branch not found in InitConfig' }
$il2 = $ic.Body.GetILProcessor()
$il2.InsertBefore($t2, [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Pop))
$il2.Replace($t2, [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Br_s, $t2.Operand))

# 3) embed the addon assembly
$addonBytes = [System.IO.File]::ReadAllBytes($AddonDll)
$res = New-Object Mono.Cecil.EmbeddedResource('PclFrpAddon.dll', ([Mono.Cecil.ManifestResourceAttributes]::Public), $addonBytes)
$mod.Resources.Add($res)

# 4) inject FrpAttachAddon()
$attr = [Mono.Cecil.MethodAttributes](([int][Mono.Cecil.MethodAttributes]::Private) -bor ([int][Mono.Cecil.MethodAttributes]::HideBySig))
$newMethod = New-Object Mono.Cecil.MethodDefinition('FrpAttachAddon', $attr, $void)
$pl.Methods.Add($newMethod)
$body = $newMethod.Body
$body.InitLocals = $true
$body.MaxStackSize = 8
$vAsm = New-Object Mono.Cecil.Cil.VariableDefinition($tAssembly); $body.Variables.Add($vAsm)
$vStream = New-Object Mono.Cecil.Cil.VariableDefinition($tStream); $body.Variables.Add($vStream)
$vBytes = New-Object Mono.Cecil.Cil.VariableDefinition($byteArray); $body.Variables.Add($vBytes)
$vAddon = New-Object Mono.Cecil.Cil.VariableDefinition($tAssembly); $body.Variables.Add($vAddon)
$vType = New-Object Mono.Cecil.Cil.VariableDefinition($tType); $body.Variables.Add($vType)
$vMethod = New-Object Mono.Cecil.Cil.VariableDefinition($tMethodInfo); $body.Variables.Add($vMethod)
$ip = $body.GetILProcessor()
$endInstr = [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ret)

$ip.Emit([Mono.Cecil.Cil.OpCodes]::Call, $mGetExecuting)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vAsm)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vAsm)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldstr, 'PclFrpAddon.dll')
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mGetResStream)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vStream)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vStream)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Brfalse, $endInstr)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vStream)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Newobj, $mBrCtor)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Dup)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vStream)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mStreamLength)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Conv_I4)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mReadBytes)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vBytes)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Pop)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vBytes)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Call, $mAsmLoadBytes)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vAddon)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vAddon)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldstr, 'PclFrpAddon.Ui')
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mGetType)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vType)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vType)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Brfalse, $endInstr)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vType)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldstr, 'Attach')
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mGetMethod)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stloc, $vMethod)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldloc, $vMethod)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldnull)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Newarr, $obj)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Dup)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Ldarg_0)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $getPanSelect)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Stelem_Ref)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Callvirt, $mInvoke)
$ip.Emit([Mono.Cecil.Cil.OpCodes]::Pop)
$ip.Append($endInstr)

# 5) call it from the constructor
$ctor = $pl.Methods | Where-Object { $_.Name -eq '.ctor' }
$cin = $ctor.Body.Instructions
$idx = -1
for ($i = 0; $i -lt $cin.Count; $i++) {
    if ($cin[$i].OpCode -eq [Mono.Cecil.Cil.OpCodes]::Call -and
        $cin[$i].Operand -is [Mono.Cecil.MethodReference] -and
        $cin[$i].Operand.Name -eq 'InitializeComponent') { $idx = $i }
}
if ($idx -lt 0) { throw 'InitializeComponent not found in PageLinkMain..ctor' }
$cil = $ctor.Body.GetILProcessor()
$cil.InsertAfter($cin[$idx], [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
$cil.InsertAfter($cin[$idx + 1], [Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Call, $newMethod))

$outDir = Split-Path -Parent $OutputExe
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
$asm.Write($OutputExe)
$asm.Dispose()
Write-Host "Patched: $OutputExe ($((Get-Item $OutputExe).Length) bytes)"
