using System;
using System.Collections.Generic;
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
			private readonly Action m_Update;

			private Bitmap m_OldBitmap;
			private Bitmap m_Bitmap;

			private int m_CurrentBitmapIndex = -1;
			private DateTime m_CrossfadeStart;
			private double m_CrossfadeProgress;
			private bool m_IsVisible;
			private float m_XOffset;
			private float m_YOffset;

			public SlideshowEngine(SlideshowWallpaperService wall) : base(wall) {
				m_Update = () => DrawFrame(false, false);
			}

			private bool UpdateBitmap() {
				string path = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures).AbsolutePath, "Wallpapers");
				List<string> wallpapers = new List<string>(Directory.GetFiles(path));

				string auxiliaryFolder = Path.Combine(path, DesiredMinimumHeight > DesiredMinimumWidth ? "Portrait" : "Landscape");
				if (Directory.Exists(auxiliaryFolder)) {
					wallpapers.AddRange(Directory.GetFiles(auxiliaryFolder));
				}

				int key = (int) (DateTime.Now - new DateTime(2020, 1, 1)).TotalMinutes;
				if (m_Shuffle) {
					key = key.ToString().GetHashCode();
				}
				int newBitmapIndex = Math.Abs(key) % wallpapers.Count;
				if (newBitmapIndex != m_CurrentBitmapIndex) {
					int oldBitmapIndex = m_CurrentBitmapIndex;
					m_CurrentBitmapIndex = newBitmapIndex;
					m_OldBitmap = m_Bitmap;
					string pathName = wallpapers[m_CurrentBitmapIndex];
					Android.Util.Log.Debug("SlideshowWallpaper", pathName);
					m_Bitmap = BitmapFactory.DecodeFile(pathName);
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

			public override void OnOffsetsChanged(float xOffset, float yOffset, float xOffsetStep, float yOffsetStep, int xPixelOffset, int yPixelOffset) {
				m_XOffset = xOffset;
				m_YOffset = yOffset;
				DrawFrame(false, true);
			}

			private void DrawFrame(bool becameVisible, bool redraw) {
				void scheduleUpdate(long millis) {
					m_Handler.RemoveCallbacks(m_Update);
					m_Handler.PostDelayed(m_Update, millis);
				}

				if (becameVisible) {
					m_OldBitmap?.Recycle();
					UpdateBitmap();
					m_CrossfadeProgress = 0;
					m_OldBitmap?.Recycle();
					DrawWallpaper(false);
					scheduleUpdate(1000);
				} else if (m_IsVisible) {
					if (m_CrossfadeProgress > 0) {
						m_CrossfadeProgress = (DateTime.Now - m_CrossfadeStart).TotalSeconds;

						if (m_CrossfadeProgress >= 1) {
							m_CrossfadeProgress = 0;

							DrawWallpaper(false);

							m_OldBitmap?.Recycle();
							m_OldBitmap = null;
							scheduleUpdate(1000);
						} else {
							m_OldBitmapPaint.SetARGB((int) ((1 - m_CrossfadeProgress) * 255), 255, 255, 255);

							DrawWallpaper(true);

							scheduleUpdate(1000 / 60);
						}
					} else {
						if (UpdateBitmap()) {
							m_CrossfadeProgress = 0.001;
							m_CrossfadeStart = DateTime.Now;

							scheduleUpdate(1000 / 60);
						} else {
							if (redraw) {
								DrawWallpaper(false);
							}
							scheduleUpdate(1000);
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
