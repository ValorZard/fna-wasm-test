/* VideoPlayer DynamicSoundEffectInstance Reverb/Filter Test Program
 * Written by Ethan "flibitijibibo" Lee
 * http://www.flibitijibibo.com/
 *
 * Released under public domain.
 * No warranty implied; use at your own risk.
 */

using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Graphics;

namespace GameCore;
public class GameMain : Game
{
	static void Main(string[] args)
	{
		using (GameMain p = new GameMain())
		{
			p.Run();
		}
	}

	GraphicsDeviceManager gdm;
	Texture2D solid;
	SpriteBatch sb;
	VideoPlayer vp;
	Video v;

	FieldInfo sfi;
	MethodInfo applyReverb;
	MethodInfo applyFilter;
	float filter, reverb;

	public GameMain() : base()
	{
		gdm = new GraphicsDeviceManager(this);

		sfi = typeof(VideoPlayer).GetField(
			"audioStream",
			BindingFlags.Instance | BindingFlags.NonPublic
		);
		applyReverb = typeof(DynamicSoundEffectInstance).GetMethod(
			"INTERNAL_applyReverb",
			BindingFlags.Instance | BindingFlags.NonPublic
		);
		applyFilter = typeof(DynamicSoundEffectInstance).GetMethod(
			"INTERNAL_applyLowPassFilter",
			BindingFlags.Instance | BindingFlags.NonPublic
		);

        // All content loaded will be in a "Content" folder
        Content.RootDirectory = "Content";
	}

	protected override void LoadContent()
	{
		sb = new SpriteBatch(GraphicsDevice);
		solid = new Texture2D(GraphicsDevice, 1, 1);
		solid.SetData(new Color[] { Color.White });
		vp = new VideoPlayer();
        // video taken from: https://commons.wikimedia.org/wiki/File:%22Amsterdam_Diamantstad%22_Weeknummer_57-27_-_Open_Beelden_-_44071.ogv
		v = Content.Load<Video>("videos/test_video");
		gdm.PreferredBackBufferWidth = v.Width;
		gdm.PreferredBackBufferHeight = v.Height;
		gdm.ApplyChanges();

		vp.Play(v);
	}

	protected override void UnloadContent()
	{
		sb.Dispose();
		solid.Dispose();
		vp.Dispose();
		v = null;
	}

	protected override void Update(GameTime gameTime)
	{
		GamePadState gp = GamePad.GetState(PlayerIndex.One);
		if (	gp.IsButtonDown(Buttons.Start) ||
			vp.State == MediaState.Stopped	)
		{
			Exit();
			return;
		}

		reverb = gp.Triggers.Left;
		filter = gp.Triggers.Right;

		object stream = sfi.GetValue(vp);
		if (stream != null)
		{
			applyReverb.Invoke(stream, new object[] { reverb });
			applyFilter.Invoke(stream, new object[]
			{
				Math.Max(1.0f - filter, 0.1f)
			});
		}

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
		sb.Draw(vp.GetTexture(), Vector2.Zero, Color.White);
		sb.Draw(
			solid,
			new Rectangle(
				0, 0,
				50, (int) (v.Height * reverb)
			),
			Color.Red
		);
		sb.Draw(
			solid,
			new Rectangle(
				v.Width - 50, 0,
				50, (int) (v.Height * filter)
			),
			Color.Blue
		);
		sb.End();
		base.Draw(gameTime);
	}
}
