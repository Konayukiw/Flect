function ConvertTo-BrandVersion([string]$Version) {
    $text = "$Version".Trim()
    if ($text -notmatch '^\d+\.\d+(?:\.\d+){0,2}$') {
        throw "BrandVersion '$Version' must use two, three, or four numbers, such as 1.2 or 1.2.1."
    }
    $pieces = @($text.Split('.') | ForEach-Object { [int]$_ })
    foreach ($piece in $pieces) {
        if ($piece -lt 0 -or $piece -gt 65535) {
            throw "BrandVersion '$Version' has a number outside 0-65535."
        }
    }
    while ($pieces.Count -lt 4) { $pieces += 0 }
    return [version]::new($pieces[0], $pieces[1], $pieces[2], $pieces[3])
}

function Format-BrandDisplayVersion([string]$Version) {
    $parsed = ConvertTo-BrandVersion $Version
    if ($parsed.Revision -gt 0) { return '{0}.{1}.{2}.{3}' -f $parsed.Major, $parsed.Minor, $parsed.Build, $parsed.Revision }
    if ($parsed.Build -gt 0) { return '{0}.{1}.{2}' -f $parsed.Major, $parsed.Minor, $parsed.Build }
    return '{0}.{1}' -f $parsed.Major, $parsed.Minor
}

function Format-BrandPackageVersion([string]$Version) {
    $parsed = ConvertTo-BrandVersion $Version
    return '{0}.{1}.{2}.{3}' -f $parsed.Major, $parsed.Minor, $parsed.Build, $parsed.Revision
}
