namespace Cross_View
{
    internal struct TextureModes
    {
        public bool MirrorX;
        public bool MirrorY;

        public bool ClampX;
        public bool ClampY;

        public TextureModes(bool mirrorX, bool mirrorY, bool clampX, bool clampY)
        {
            MirrorX = mirrorX;
            MirrorY = mirrorY;

            ClampX = clampX;
            ClampY = clampY;
        }
    }
}
