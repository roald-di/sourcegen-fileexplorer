**Proof of concept** source generator that generates string constants for every file in a specified directory

Sample usage
------------

add this to your .csproj
```xml
  <ItemGroup>
    <AdditionalFiles Include="assets\**\*" RelativeTo="FileExplorerUsage" BrowseFrom="FileExplorerUsage/assets/mobile" TypeName="FileExplorerUsage.Definitions.MobileAssets" />
    <AdditionalFiles Include="lang\**\*" RelativeTo="FileExplorerUsage/" BrowseFrom="FileExplorerUsage/lang" TypeName="FileExplorerUsage.Definitions.Languages" />
  </ItemGroup>
```

to get this
```cs
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(MobileAssets.App.Ios.Dialog.Cancel_0_1_png);
        Console.WriteLine(MobileAssets.Sound.Aac.Damage.Fallsmall_m4a);
        Console.WriteLine(Languages.En_US_lang);
    }
}
```

output on windows:
```
assets\mobile\app\ios\dialog\cancel_0_1.png
assets\mobile\sound\aac\damage\fallsmall.m4a
lang\en_US.lang
```

output on linux:
```
assets/mobile/app/ios/dialog/cancel_0_1.png
assets/mobile/sound/aac/damage/fallsmall.m4a
lang/en_US.lang
```