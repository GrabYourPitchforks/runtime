//// Licensed to the .NET Foundation under one or more agreements.
//// The .NET Foundation licenses this file to you under the MIT license.
//// See the LICENSE file in the project root for more information.

//using System.Buffers;
//using System.Diagnostics;
//using System.IO;
//using System.Numerics;
//using System.Runtime.CompilerServices;
//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;

//namespace System.Text.Encodings.Web
//{
//    internal sealed class AsciiTextEncode<TImpl> : TextEncoder
//        where TImpl : struct, IEncoderImplementation
//    {
//        private readonly Sse41AsciiEncoder<TImpl> _encoder;

//        public override int MaxOutputCharactersPerInputCharacter => _encoder._encoder.MaxOutputCharsPerInputRune;

//        public override void Encode(TextWriter output, char[] value, int startIndex, int characterCount)
//        {
//            if (output is null)
//            {
//                throw new ArgumentNullException(nameof(output));
//            }

//            _encoder.Encode(value.AsSpan(startIndex, characterCount), output);
//        }

//        public override OperationStatus Encode(ReadOnlySpan<char> source, Span<char> destination, out int charsConsumed, out int charsWritten, bool isFinalBlock = true)
//        {
//#error not implemented
//        }

//        public override string Encode(string value)
//        {
//            if (value is null)
//            {
//                throw new ArgumentNullException(nameof(value));
//            }

//            return _encoder.Encode(value);
//        }

//        public override void Encode(TextWriter output, string value, int startIndex, int characterCount)
//        {
//            if (output is null)
//            {
//                throw new ArgumentNullException(nameof(output));
//            }

//            _encoder.Encode(value.AsSpan(startIndex, characterCount), output);
//        }

//        public override OperationStatus EncodeUtf8(ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
//        {
//#error not implemented
//        }

//        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
//        {
//            if (text == null)
//            {
//                if (textLength != 0)
//                {
//#error throw exception here
//                }
//            }
//            else if (textLength < 0)
//            {
//#error throw exception here
//            }

//            return _encoder.FindIndexOfFirstCharToBeEncoded(new ReadOnlySpan<char>(text, textLength));
//        }

//        public override int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
//            => _encoder.FindIndexOfFirstByteToBeEncoded(utf8Text);

//        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
//        {
//#error not implemented
//        }

//        public override bool WillEncode(int unicodeScalar)
//            => _encoder.WillEncode((uint)unicodeScalar);
//    }
//}
