# Prefab Addressables Setup

This project now supports an Addressables-ready prefab catalog while keeping the old direct prefab lists as fallback.

## 1. Install Addressables

In Unity:

1. Open `Window > Package Manager`.
2. Search for `Addressables`.
3. Install `com.unity.addressables`.
4. Open `Window > Asset Management > Addressables > Groups`.
5. Click `Create Addressables Settings` if Unity asks.

The runtime code does not directly reference the package, so the project still compiles before this step.

## 2. Configure `MainMenuPrefabCatalog`

Use or create a catalog asset:

1. Select `Assets/MainMenuPrefabCatalog.asset`.
2. In `Character Entries`, add one entry per prefab in `Assets/Prefabs/Cast`.
3. In `Instrument Entries`, add one entry per prefab in `Assets/Prefabs/Intrument`.
4. For each entry:
   - `Id`: stable saved id, usually the prefab name.
   - `Address Key`: the Addressables address, usually the same as `Id`.
   - `Display Name`, `Category`, `Avatar`: UI metadata so menus do not need to load the prefab.
   - `Default Animation Id` and `Animation Ids`: needed for address-only character entries.
   - `Prefab`: keep this during migration as fallback; clear it after Addressables is verified if you want to remove direct build references.

## 3. Mark prefabs Addressable

For every character and instrument prefab:

1. Select the prefab.
2. Enable `Addressable` in the Inspector.
3. Set its address to match the catalog `Address Key`.
4. Put characters in a group like `Characters_Local`.
5. Put instruments in a group like `Instruments_Local`.

Recommended address format:

- Characters: prefab name, for example `BUNNY`, `CAT`, `FRANCE`.
- Instruments: configured `AudioConfig.Name` or prefab name, for example `DrumBot`, `AquaKeyboard`.

Keep addresses stable because saved characters store these ids.

## 4. Assign the catalog

In the scene that contains `MainMenuDataManager`:

1. Select the `MainMenuDataManager` object.
2. Assign `Assets/MainMenuPrefabCatalog.asset` to `Prefab Catalog`.
3. Keep the old `Character Prefabs` and `Instrument Prefabs` lists while testing.
4. After Addressables works, move data fully into catalog entries and clear direct prefab references that you no longer want in the base build.

## 5. Build Addressables

Before making a player build:

1. Open `Window > Asset Management > Addressables > Groups`.
2. Choose `Build > New Build > Default Build Script`.
3. Then build the app normally.

If a prefab fails to load from Addressables, the runtime tries the direct prefab fallback and then `Resources.Load`.
