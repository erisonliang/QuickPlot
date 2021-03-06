﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using SkiaSharp;

namespace QuickPlot.Forms
{
    public partial class InteractivePlot : UserControl
    {
        public Figure fig = new Figure();

        SKCanvas canvas;
        Control control;

        public readonly bool IsUsingOpenGL;

        SkiaSharp.Views.Desktop.SKControl skControl1;
        OpenTK.GLControl glControl1;

        SKColorType colorType = SKColorType.Rgba8888;
        GRBackendRenderTarget renderTarget;
        SKSurface surface;
        GRContext context;

        public InteractivePlot()
        {
            InitializeComponent();
            Disposed += OnDispose;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                lblMessage.Text = "QuickPlot InteractivePlot\n(inside Visual Studio)";
            }
            else
            {

                try
                {
                    CreateControl(true);
                    IsUsingOpenGL = true;
                }
                catch
                {
                    CreateControl(false);
                    IsUsingOpenGL = false;
                }

                lblMessage.Visible = true;
                lblMessage.BringToFront();
                lblMessage.Text = $"";
            }
        }

        private void OnDispose(Object sender, EventArgs e)
        {
            renderTarget?.Dispose();
            surface?.Dispose();
            context?.Dispose();
        }

        private void CreateControl(bool useOpenGL = false)
        {
            if (useOpenGL)
            {
                ColorFormat colorFormat = new ColorFormat(8, 8, 8, 8);
                int depth = 24;
                int stencil = 8;
                int samples = 4;
                GraphicsMode graphicsMode = new GraphicsMode(colorFormat, depth, stencil, samples);
                glControl1 = new OpenTK.GLControl(graphicsMode)
                {
                    BackColor = Color.FromArgb(0, 0, 192),
                    VSync = true,
                    Dock = DockStyle.Fill
                };
                glControl1.Paint += new PaintEventHandler(GlControl1_Paint);
                control = glControl1;

            }
            else
            {
                skControl1 = new SkiaSharp.Views.Desktop.SKControl
                {
                    BackColor = Color.FromArgb(192, 0, 0),
                    Dock = DockStyle.Fill
                };
                skControl1.PaintSurface += new EventHandler<SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs>(SkControl1_PaintSurface);
                control = skControl1;
            }

            Controls.Add(control);
            control.BringToFront();
            control.Update();

            control.MouseMove += new MouseEventHandler(OnMouseMove);
            control.MouseDown += new MouseEventHandler(OnMouseDown);
            control.MouseUp += new MouseEventHandler(OnMouseUp);
            control.MouseWheel += new MouseEventHandler(OnMouseWheel);
            control.MouseClick += new MouseEventHandler(OnMouseClick);
        }

        private void SkControl1_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
        {
            canvas = e.Surface.Canvas;
            fig.Render(canvas);
        }

        private void GlControl1_Paint(object sender, PaintEventArgs e)
        {

            Control senderControl = (Control)sender;

            if (context == null)
            {
                var glInterface = GRGlInterface.CreateNativeGlInterface();
                context = GRContext.Create(GRBackend.OpenGL, glInterface);
            }

            if (renderTarget == null || surface == null || renderTarget.Width != senderControl.Width || renderTarget.Height != senderControl.Height)
            {
                renderTarget?.Dispose();

                GL.GetInteger(GetPName.FramebufferBinding, out var framebuffer);
                GL.GetInteger(GetPName.StencilBits, out var stencil);
                var glInfo = new GRGlFramebufferInfo((uint)framebuffer, colorType.ToGlSizedFormat());
                renderTarget = new GRBackendRenderTarget(senderControl.Width, senderControl.Height, context.GetMaxSurfaceSampleCount(colorType), stencil, glInfo);
                surface?.Dispose();
                surface = SKSurface.Create(context, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
            }

            canvas = surface.Canvas;
            fig.Render(canvas);

            surface.Canvas.Flush();
            glControl1.SwapBuffers();
        }

        #region click-drag mouse panning and zooming

        Plot plotBeingClicked = null;

        public void OnMouseDown(object sender, MouseEventArgs e)
        {
            SKPoint mouseLocation = new SKPoint(e.Location.X, e.Location.Y);
            plotBeingClicked = fig.GetPlotUnderCursor(canvas, mouseLocation);

            if (plotBeingClicked != null)
            {
                if (e.Button == MouseButtons.Left)
                    plotBeingClicked.mouse.LeftDown(mouseLocation);
                else if (e.Button == MouseButtons.Right)
                    plotBeingClicked.mouse.RightDown(mouseLocation);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            SKPoint mouseLocation = new SKPoint(e.Location.X, e.Location.Y);

            if (plotBeingClicked != null)
            {
                if (e.Button == MouseButtons.Left)
                {
                    plotBeingClicked.mouse.LeftLocation(mouseLocation);

                }
                else if (e.Button == MouseButtons.Right)
                {
                    plotBeingClicked.mouse.RightLocation(mouseLocation);
                }
                control.Refresh();
            }
        }

        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (plotBeingClicked != null)
            {
                if (e.Button == MouseButtons.Left)
                {
                    plotBeingClicked.axes.PanPixels(plotBeingClicked.mouse.leftDownDelta);
                    plotBeingClicked.mouse.LeftUp();
                }
                else if (e.Button == MouseButtons.Right)
                {
                    plotBeingClicked.axes.ZoomPixels(plotBeingClicked.mouse.rightDownDelta);
                    plotBeingClicked.mouse.RightUp();
                }
                plotBeingClicked = null;
            }

            control.Refresh();
        }

        public void OnMouseWheel(object sender, MouseEventArgs e)
        {
            SKPoint mouseLocation = new SKPoint(e.Location.X, e.Location.Y);
            plotBeingClicked = fig.GetPlotUnderCursor(canvas, mouseLocation);

            if (plotBeingClicked != null)
            {
                double zoomFrac = (e.Delta > 0) ? 1.15 : .85;
                plotBeingClicked.axes.Zoom(zoomFrac, zoomFrac);
            }

            plotBeingClicked = null;
            control.Refresh();
        }

        public void OnMouseClick(object sender, MouseEventArgs e)
        {
            SKPoint mouseLocation = new SKPoint(e.Location.X, e.Location.Y);

            if (e.Button == MouseButtons.Middle)
            {
                plotBeingClicked = fig.GetPlotUnderCursor(canvas, mouseLocation);
                if (plotBeingClicked != null)
                {
                    plotBeingClicked.mouse.LeftUp();
                    plotBeingClicked.mouse.RightUp();
                    if (e.Button == MouseButtons.Middle)
                        plotBeingClicked.AutoAxis();
                }
                plotBeingClicked = null;
                control.Refresh();
            }
        }

        #endregion
    }
}
