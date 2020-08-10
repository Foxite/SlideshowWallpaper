using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Service.Wallpaper;
using Android.Views;
using Path = System.IO.Path;

namespace SlideshowWallpaper {
	[Service(Label = "@string/app_name", Permission = "android.permission.BIND_WALLPAPER")]
	[IntentFilter(new string[] { "android.service.wallpaper.WallpaperService" })]
	[MetaData("android.service.wallpaper", Resource = "@xml/slideshow")]
	public partial class SlideshowWallpaperService : WallpaperService {
		public override Engine OnCreateEngine() {
			return new SlideshowEngine(this);
		}

		private class SlideshowEngine : Engine {
			private readonly Paint m_OldBitmapPaint = new Paint();
			private readonly Paint m_Paint = new Paint();
			private readonly Handler m_Handler = new Handler();
			private readonly bool m_Shuffle = false;
			private Bitmap m_OldBitmap;
			private Bitmap m_Bitmap;
			private bool m_IsVisible;
			private int m_CurrentBitmapIndex = -1;

			public SlideshowEngine(SlideshowWallpaperService wall) : base(wall) { }

			private bool UpdateBitmap() {
				string[] wallpapers = Directory.GetFiles(Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures).AbsolutePath, "Wallpapers"));
				int key = (int) (DateTime.Now - new DateTime(2020, 1, 1)).TotalMinutes;
				if (m_Shuffle) {
					key = key.ToString().GetHashCode();
				}
				int newBitmapIndex = Math.Abs(key) % wallpapers.Length;
				if (newBitmapIndex != m_CurrentBitmapIndex) {
					int oldBitmapIndex = m_CurrentBitmapIndex;
					m_CurrentBitmapIndex = newBitmapIndex;
					m_OldBitmap = m_Bitmap;
					m_Bitmap = BitmapFactory.DecodeFile(wallpapers[m_CurrentBitmapIndex]);
					return oldBitmapIndex != -1;
				} else {
					return false;
				}
			}

			public override void OnVisibilityChanged(bool visible) {
				bool wasVisible = m_IsVisible;
				m_IsVisible = visible;

				if (visible) {
					DrawFrame(wasVisible != visible, false);
				}
			}

			public override void OnDestroy() {
				m_OldBitmap?.Dispose();
				m_Bitmap.Dispose();
			}

			public override void OnSurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) {
				base.OnSurfaceChanged(holder, format, width, height);

				DrawFrame(true, false);
			}

			private float m_XOffset;
			private float m_YOffset;

			public override void OnOffsetsChanged(float xOffset, float yOffset, float xOffsetStep, float yOffsetStep, int xPixelOffset, int yPixelOffset) {
				m_XOffset = xOffset;
				m_YOffset = yOffset;
				DrawFrame(false, true);
			}

			private double m_CrossfadeProgress;
			private DateTime m_CrossfadeStart;

			private void DrawFrame(bool becameVisible, bool redraw) {
				if (becameVisible) {
					m_OldBitmap?.Recycle();
					UpdateBitmap();
					m_CrossfadeProgress = 0;
					m_OldBitmap?.Recycle();
					DrawWallpaper(false);
				} else if (m_IsVisible) {
					if (m_CrossfadeProgress > 0) {
						m_CrossfadeProgress = (DateTime.Now - m_CrossfadeStart).TotalSeconds;

						if (m_CrossfadeProgress >= 1) {
							m_CrossfadeProgress = 0;

							DrawWallpaper(false);

							m_OldBitmap?.Recycle();
							m_OldBitmap = null;
						} else {
							m_OldBitmapPaint.SetARGB((int) ((1 - m_CrossfadeProgress) * 255), 255, 255, 255);

							DrawWallpaper(true);

							m_Handler.PostDelayed(() => DrawFrame(false, false), 1000 / 60);
						}
					} else {
						if (UpdateBitmap()) {
							m_CrossfadeProgress = 0.001;
							m_CrossfadeStart = DateTime.Now;

							m_Handler.PostDelayed(() => DrawFrame(false, false), 1000 / 60);
						} else if (redraw) {
							DrawWallpaper(false);
						} else {
							m_Handler.PostDelayed(() => DrawFrame(false, false), 1000);
						}
					}
				} else {
					m_CrossfadeProgress = 0;
					m_OldBitmap?.Recycle();
					m_OldBitmap = null;
				}
			}

			private void DrawWallpaper(bool crossfadeOld) {
				ISurfaceHolder holder = SurfaceHolder;
				Canvas c = null;

				try {
					c = holder.LockCanvas();

					if (c != null) {
						DrawBitmap(c, m_Bitmap, m_Paint);
						if (crossfadeOld) {
							DrawBitmap(c, m_OldBitmap, m_OldBitmapPaint);
						}
					}
				} finally {
					if (c != null) {
						holder.UnlockCanvasAndPost(c);
					}
				}
			}

			private void DrawBitmap(Canvas c, Bitmap bitmap, Paint paint) {
				var m = new Matrix();

				// Scale to fill
				float scale = MathF.Max((float) c.Width / bitmap.Width, (float) c.Height / bitmap.Height);
				m.PostScale(scale, scale);

				// Offset
				m.PostTranslate(-m_XOffset * (bitmap.Width * scale - c.Width), -m_YOffset * (bitmap.Height * scale - c.Height));

				c.DrawBitmap(bitmap, m, paint);
			}
		}
	}
}
