namespace Cross_View.Parser
{
    internal struct N64Combiner
    {
        public int SubA;
        public int SubB;
        public int Mul;
        public int Add;
    }

    internal class Combiners
    {
        public N64Combiner[] ColorCombiner;
        public N64Combiner[] AlphaCombiner;

        public Combiners()
        {
            ColorCombiner = new[] {new N64Combiner(), new N64Combiner()};
            AlphaCombiner = new[] {new N64Combiner(), new N64Combiner()};
        }
    }
}
