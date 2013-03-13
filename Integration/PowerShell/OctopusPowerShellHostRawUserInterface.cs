using System;
using System.Management.Automation.Host;

namespace Octopus.Shared.Integration.PowerShell
{
    public sealed class OctopusPowerShellHostRawUserInterface : PSHostRawUserInterface
    {
        public OctopusPowerShellHostRawUserInterface()
        {
            ForegroundColor = ConsoleColor.Black;
            BackgroundColor = ConsoleColor.White;
        }

        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            throw new NotSupportedException("ReadKey is not supported by the Octopus PowerShell host");
        }

        public override void FlushInputBuffer()
        {
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            return new BufferCell[rectangle.Right - rectangle.Left, rectangle.Top - rectangle.Bottom];
        }

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
        }

        public override ConsoleColor ForegroundColor { get; set; }

        public override ConsoleColor BackgroundColor { get; set; }

        public override Coordinates CursorPosition { get; set; }

        public override Coordinates WindowPosition
        {
            get { throw new NotSupportedException("WindowPosition is not supported by the Octopus PowerShell host"); }
            set { throw new NotSupportedException("WindowPosition is not supported by the Octopus PowerShell host"); }
        }

        public override int CursorSize
        {
            get { throw new NotSupportedException("CursorSize is not supported by the Octopus PowerShell host"); }
            set { throw new NotSupportedException("CursorSize is not supported by the Octopus PowerShell host"); }
        }

        public override Size BufferSize
        {
            get { return new Size(600, 0); }
            set { }
        }

        public override Size WindowSize
        {
            get { throw new NotSupportedException("WindowSize is not supported by the Octopus PowerShell host"); }
            set { throw new NotSupportedException("WindowSize is not supported by the Octopus PowerShell host"); }
        }

        public override Size MaxWindowSize
        {
            get { throw new NotSupportedException("MaxWindowSize is not supported by the Octopus PowerShell host"); }
        }

        public override Size MaxPhysicalWindowSize
        {
            get { throw new NotSupportedException("MaxPhysicalWindowSize is not supported by the Octopus PowerShell host"); }
        }

        public override bool KeyAvailable
        {
            get { return false; }
        }

        public override string WindowTitle { get; set; }
    }
}