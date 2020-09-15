using System.Collections;
using System.Text;
using UnityEngine;

namespace Backtrace.Unity.Model
{
    internal class CoroutineStringBuilder
    {
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private const int defaultYield = 1024 * 64;
        private int nextYield = defaultYield;


        internal void Append(string text)
        {
            _stringBuilder.Append(text);
        }

        internal void AppendFormat(string format, params object[] args)
        {
            _stringBuilder.AppendFormat(format, args);
        }

        internal bool ShouldYield()
        {
            return _stringBuilder.Length >= nextYield;
        }

        internal IEnumerator WaitForFrame()
        {

            yield return new WaitForEndOfFrame();
            nextYield += defaultYield;
        }


        public override string ToString()
        {
            return _stringBuilder.ToString();
        }
    }
}
