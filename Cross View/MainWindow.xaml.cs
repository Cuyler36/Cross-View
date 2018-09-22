using System.IO;
using System.Windows;
using Cross_View.Parser;
using Microsoft.Win32;
using SharpGL;
using SharpGL.SceneGraph;

namespace Cross_View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private float posX = 0.0f, posY = -5.0f, posZ = -25.0f;
        private int a = 0;
        private FileStream _ramStream;
        private BinaryReader _ramReader;
        private F3DACEXParser _parser;
        private long _baseAddress;

        public MainWindow()
        {
            InitializeComponent();
            GlControl.OpenGLDraw += OnOpenGLDraw;
            GlControl.OpenGLInitialized += Initialize;
            GlControl.Resized += Resizing;
        }

        private void OnOpenGLDraw(object sender, OpenGLEventArgs e)
        {
            var gl = e.OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            gl.ClearColor(0.5f, 0.5f, 0.5f, 0.0f);
            gl.LoadIdentity();

            //  Move the geometry into a fairly central position.
            gl.Translate(-1.5f, 0.0f, -6.0f);

            _parser?.ParseModel(_baseAddress, gl);

            gl.Flush();
        }

        private void OpenGLDrawTest(object sender, OpenGLEventArgs e)
        {
            var gl = e.OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
            gl.LoadIdentity();

            gl.Translate(posX, posY, posZ);

            gl.Begin(OpenGL.GL_POLYGON);
            gl.Color(0.0f, 1.0f, 0.0f);
            gl.Vertex(0.0f, 1.0f, 1.0f);
            gl.Vertex(4.0f, 2.0f, 1.0f);
            gl.Vertex(5.0f, 10.0f, 1.0f);
            gl.Vertex(1.5f, 11.0f, 1.0f);
            gl.Vertex(-1.5f, 11.0f, 1.0f);
            gl.Vertex(-5.0f, 10.0f, 1.0f);
            gl.Vertex(-4.0f, 2.0f, 1.0f);

            gl.End();
        }

        private void Initialize(object sender, OpenGLEventArgs e)
        {
            e.OpenGL.Enable(OpenGL.GL_DEPTH_TEST);
        }

        private void Resizing(object sender, OpenGLEventArgs e)
        {
            var gl = e.OpenGL;
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Perspective(45.0f, (float) gl.RenderContextProvider.Width / gl.RenderContextProvider.Height, 0.001f, 10000.0f);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
        }

        private void LoadModelFromRamDump(string dumpLocation)
        {
            if (!File.Exists(dumpLocation)) return;
            var dlAddressBox = new DrawListAddressBox();
            var result = dlAddressBox.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            if (_ramReader != null)
            {
                _ramReader.Close();
                _ramStream.Dispose();
                _ramReader.Dispose();
            }

            _baseAddress = dlAddressBox.Address & ~0x80000000;
            _ramStream = new FileStream(dumpLocation, FileMode.Open);
            _ramReader = new BinaryReader(_ramStream);

            _ramStream.Seek(_baseAddress, SeekOrigin.Begin);
            _parser = new F3DACEXParser(GlControl.OpenGL, _ramReader);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (!result.HasValue || !result.Value) return;

            var modelFileInfo = new FileInfo(dialog.FileName);
            if (modelFileInfo.Length == 0x01800000)
            {
                LoadModelFromRamDump(dialog.FileName);
            }
        }

        private void Add_Model_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (ModelGroup != null)
            {
                if (RAMReader == null)
                {
                    if (Model_Select_Dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string File_Name = Model_Select_Dialog.FileName;
                        if (!File_Name.ToLower().Contains("model"))
                            return;

                        string Model_Path = File_Name;
                        if (File.Exists(Model_Path))
                        {
                            byte[] Model_Data = File.ReadAllBytes(Model_Path);

                            ModelParser.ParseModel(Model_Data, Points, this);

                            ModelVisualizer.Content = ModelGroup;
                            viewPort3d.ZoomExtents();
                        }
                        else
                        {
                            MessageBox.Show(Model_Path);
                        }
                    }
                }
                else
                {
                    var DLAddressBox = new DrawListAddressBox();
                    if (DLAddressBox.ShowDialog().Value)
                    {
                        uint DrawListModelAddress = DLAddressBox.Address & ~0x80000000;

                        RAMReader.BaseStream.Seek(DrawListModelAddress, SeekOrigin.Begin);

                        ModelParser.ParseModel(RAMReader, this);

                        ModelVisualizer.Content = ModelGroup;
                        viewPort3d.ZoomExtents();
                    }
                }
            }
            else
            {
                MessageBox.Show("Couldn't find the _model file!");
            }
            */
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            /*
            var saveFileDialog = new SaveFileDialog
            {
                Filter = Exporters.Filter,
                DefaultExt = Exporters.DefaultExtension
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string SaveFolder = Path.GetDirectoryName(saveFileDialog.FileName);

                // Save Textures
                foreach (KeyValuePair<string, BitmapSource> Image in TextureList)
                {
                    using (var FStream = new FileStream(SaveFolder + "\\" + Image.Key + ".png", FileMode.Create))
                    {
                        var Encoder = new PngBitmapEncoder();
                        Encoder.Frames.Add(BitmapFrame.Create(Image.Value));
                        Encoder.Save(FStream);
                    }
                }

                // Save Models
                using (FileStream Stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    //var MP = ModelPoints.Points;
                    //ModelPoints.Points = null; // Don't export vertices
                    var ModelExporter = Exporters.Create(saveFileDialog.FileName);
                    if (ModelExporter is ObjExporter)
                    {
                        var OExporter = ModelExporter as ObjExporter;
                        OExporter.MaterialsFile = Path.GetDirectoryName(saveFileDialog.FileName) + Path.DirectorySeparatorChar
                            + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + "_Material.mtl";

                        foreach (var Mat in ModelGroup.Children)
                        {
                            Console.WriteLine("Material: " + (Mat as GeometryModel3D).Material.GetName());
                        }
                    }

                    ModelExporter.Export(ModelGroup, Stream);
                    //ModelPoints.Points = MP;
                }
            }*/
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Import texture files (this means converting it from the AC format to a bitmap)
        }
    }
}
