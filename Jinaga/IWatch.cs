﻿using System.Threading.Tasks;

namespace Jinaga
{
    public interface IWatch
    {
        Task Initialized { get; }

        void Stop();
    }
}