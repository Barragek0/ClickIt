# Delve encounter textures

The encounter WebP files are the transparent Path of Exile chart sprites hosted in [PoEDB's Delve encounter table](https://poedb.tw/us/Delve_league). Artwork belongs to Grinding Gear Games. The filenames match the chart's encounter image keys.

Reveal Map uses the textures from the plugin DLL when available. If an embedded texture cannot be read, ClickIt downloads the corresponding file from this directory on the repository's main branch. Successful loads are cached for the current plugin build. If both sources fail, the chart shows an error and uses a marker in place of the icon.

- `Currency.webp` and `Maps.webp` use the original Delve chart artwork for those encounters.
- `Chaos.webp` is the purple chaos spiral.
- `BossAbyss.webp`, `BossProtoVaal.webp`, and `BossVaalCity.webp` are the Lich's Tomb, Aul's throne, and Grand Architect's Temple icons.
