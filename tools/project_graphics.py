#!/usr/bin/env python3
"""Retain the supplied renderer; adapt only its internal framework boundaries."""
from pathlib import Path
import hashlib
import json
from build_content import ROOT

NAMES = ['SpriteBatch', 'SpriteBatcher', 'SpriteBatchItem', 'SpriteEffect', 'DxtUtil', 'Texture2DReader']

def main():
    source = ROOT / 'src/Recovered/Graphics/Microsoft.Xna.Framework.Graphics'
    destination = ROOT / '.port-cache/graphics-browser'
    destination.mkdir(parents=True, exist_ok=True)
    report = []
    for name in NAMES:
        original_path = (ROOT / 'src/Recovered/Content/Microsoft.Xna.Framework.Content/Texture2DReader.cs') if name == 'Texture2DReader' else source / f'{name}.cs'
        original = original_path.read_text()
        body = '#nullable disable\nusing Microsoft.Xna.Framework;\nusing Microsoft.Xna.Framework.Graphics;\nusing Microsoft.Xna.Framework.Content;\nusing Microsoft.Xna.Platform.Graphics;\n' + original.replace(
            'namespace Microsoft.Xna.Framework.Graphics;', 'namespace StardewBrowser.Framework.Graphics;').replace(
            'namespace Microsoft.Xna.Framework.Content;', 'namespace StardewBrowser.Framework.Graphics;')
        if name == 'SpriteBatch':
            body = body.replace('public SpriteBatch(GraphicsDevice graphicsDevice, int capacity)\n\t{',
                                'public SpriteBatch(GraphicsDevice graphicsDevice, int capacity)\n\t\t: base(graphicsDevice)\n\t{')
            body = body.replace('\t\tbase.GraphicsDevice = graphicsDevice;\n', '')
            for texture in ['spriteFont.Texture', 'texture']:
                for property_name in ['TexelWidth', 'TexelHeight', 'SortingKey', 'Width', 'Height']:
                    body = body.replace(f'{texture}.{property_name}', f'TextureMetrics.{property_name}({texture})')
                for lower, upper in [('width', 'Width'), ('height', 'Height')]:
                    body = body.replace(f'{texture}.{lower}', f'TextureMetrics.{upper}({texture})')
            body = body.replace('fixed (SpriteFont.Glyph* glyphs = spriteFont.Glyphs)\n\t\t', '')
            body = body.replace('int glyphIndexOrDefault = spriteFont.GetGlyphIndexOrDefault(c);\n\t\t\t\tSpriteFont.Glyph* ptr = glyphs + glyphIndexOrDefault;',
                                'SpriteFont.Glyph glyph = RendererBoundary.Glyph(spriteFont, c);')
            body = body.replace('ptr->', 'glyph.')
            body = body.replace('SpriteFont.CharacterSource text2 = new SpriteFont.CharacterSource(text);\n\t\t\tspriteFont.MeasureString(ref text2, out var size);',
                                'var size = spriteFont.MeasureString(text);')
        elif name == 'SpriteBatcher':
            body = body.replace('_device._graphicsMetrics._spriteCount += num2;', 'RendererBoundary.SpritesSubmitted += num2;')
        elif name == 'SpriteEffect':
            body = body.replace('EffectResource.SpriteEffect.Bytecode', 'RendererBoundary.SpriteEffectBytecode')
            body = body.replace('protected internal override void OnApply()', 'protected override void OnApply()')
        elif name == 'Texture2DReader':
            body = body.replace('protected internal override Texture2D Read(', 'protected override Texture2D Read(')
            body = body.replace('reader.GetGraphicsDevice().GraphicsCapabilities', '((IPlatformGraphicsDevice)reader.GetGraphicsDevice()).Strategy.Capabilities')
            # The recovered reader accesses its captured reader through obj.
            body = body.replace('obj.((IPlatformGraphicsDevice)reader.GetGraphicsDevice()).Strategy.Capabilities', '((IPlatformGraphicsDevice)obj.reader.GetGraphicsDevice()).Strategy.Capabilities')
            body = body.replace('((IPlatformGraphicsDevice)obj.reader.GetGraphicsDevice()).Strategy.Capabilities.SupportsNonPowerOfTwo', 'true') # WebGL2 / HiDef supports NPOT mipmaps.
            body = body.replace('MathHelper.IsPowerOfTwo(', 'RendererBoundary.IsPowerOfTwo(')
            body = body.replace('ContentManager.ScratchBufferPool.Get(num)', 'new byte[num]')
            body = body.replace('reader.Read(array, 0, num);', 'reader.BaseStream.ReadExactly(array);')
            body = body.replace('ContentManager.ScratchBufferPool.Return(array);', '')
            body = body.replace('surfaceFormat.GetSize()', '4') # NormalizedByte4 is four bytes per pixel.
            body = body.replace('obj.texture.SetImageSize(num3, num4);', 'TextureMetrics.SetImageSize(obj.texture, num3, num4);')
            body = body.replace('Threading.BlockOnUIThread(obj._003CRead_003Eb__0);', 'OriginalContentReaders.TextureReads++;\n\t\tobj._003CRead_003Eb__0();')
        elif name == 'DxtUtil':
            body = body.replace('6u => 0,', '6u => (byte)0,') # Preserve original byte result; repair decompiler switch inference.
        target = destination / f'{name}.cs'
        if not target.exists() or target.read_text() != body: target.write_text(body)
        report.append({'name': name, 'original_sha256': hashlib.sha256(original.encode()).hexdigest(),
                       'projected_sha256': hashlib.sha256(body.encode()).hexdigest()})
    (destination / 'source-projection.json').write_text(json.dumps(report, indent=2) + '\n')
    print('Projected original sprite renderer, packed texture reader and DXT decoder; GPU resources supplied by KNI.')

if __name__ == '__main__': main()
