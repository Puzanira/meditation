using UnityEditor;
using UnityEngine;

namespace Meditation.EditorTools
{
    /// <summary>
    /// Import rules for the art drop, applied by the importer instead of by hand.
    ///
    /// The drop is a folder of PNGs — plates and finished screens at 1920×1080, sprites and buttons the
    /// designer exported at 2–4× their game size — plus, since 2026-08-07, seven MP3s (see
    /// <see cref="OnPreprocessAudio"/>). Every file of a kind needs the same handful of settings, and
    /// the settings differ per kind rather than per file. Doing that through an
    /// <see cref="AssetPostprocessor"/> rather than clicking means a re-import, a fresh clone or a new
    /// file from the designer all land on the same settings, and the reason for each one is written
    /// down here rather than living only in a .meta.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private static string _artRoot;

        /// <summary>
        /// Where THIS game's art lives, right now.
        ///
        /// A postprocessor is global: it is offered every texture the editor imports, including the
        /// ones belonging to whatever other project is hosting us. And hosting is the normal case —
        /// <c>_Project</c> ships to the cabinet as the Unity package
        /// <c>com.aigamestudio.game-meditation</c>, so inside the arcade hub this same class is loaded
        /// from <c>Packages/com.aigamestudio.game-meditation/</c> while <c>Assets/_Project/Art/</c>, if
        /// the hub ever makes one, is somebody else's folder entirely. Asking the package manager where
        /// this assembly came from answers both cases exactly: a package path when packaged, null (and
        /// the project's own folder) when this IS the project.
        /// </summary>
        private static string ArtRoot
        {
            get
            {
                if (_artRoot != null) return _artRoot;

                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(ArtImportSettings).Assembly);
                _artRoot = (package != null ? package.assetPath : "Assets/_Project") + "/Art/";
                return _artRoot;
            }
        }

        /// <summary>
        /// The seven tracks of MECHANICS §8, imported the way the spec asks for: **streaming**, not
        /// decompress-on-load.
        ///
        /// Not a preference. The library's track is 18 MB of MP3 and the metro's is 11; decompressed
        /// into memory that is hundreds of megabytes of PCM sitting in a cabinet process that hosts
        /// seven games, and Unity would decode all of it before the first level card. Streaming reads
        /// it off disk as it plays, which is exactly what a looping ambience wants, and
        /// <c>preloadAudioData = false</c> keeps the level's own track from being loaded until that
        /// level starts.
        /// </summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;

            // Decoding an 18 MB track must not hold the first frame of a level.
            importer.loadInBackground = true;

            // A cabinet speaker plays the same mix to the room; a stereo ambience stays stereo.
            importer.forceToMono = false;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            // Sprites are trimmed to their content on import (see the import script), and the game
            // places every one of them by an explicit design-pixel rectangle through UGUI — so pixels
            // per unit never enters the arithmetic. It is pinned only so a future re-import cannot
            // change a size behind the catalogue's back.
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            // Soft blurred edges are the drop's style (SCREENS «Технические заметки»), and the
            // silhouettes are drawn well below their native size — bilinear, and alpha treated as
            // transparency so the halo does not fringe.
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;

            // No mips: everything is drawn at a fixed size on a fixed 1920×1080 frame, and a mip chain
            // would only soften details that are already small.
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;

            // 2048 covers the 1920-wide plates and the largest thought canvas (1815×1412) without
            // downscaling; compression stays high-quality because these are flat-colour vectors where
            // block artefacts show up immediately on a gradient sky.
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.maxTextureSize = 2048;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
