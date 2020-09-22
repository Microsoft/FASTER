﻿using System;
using System.Collections.Generic;

namespace FASTER.core
{
    /// <summary>
    /// Callback functions to FASTER
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <typeparam name="Input"></typeparam>
    /// <typeparam name="Output"></typeparam>
    /// <typeparam name="Context"></typeparam>
    public interface IFunctions<Key, Value, Input, Output, Context>
    {
        /// <summary>
        /// Read completion
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="ctx"></param>
        /// <param name="status"></param>
        void ReadCompletionCallback(ref Key key, ref Input input, ref Output output, ref Context ctx, Status status);

        /// <summary>
        /// Upsert completion
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="ctx"></param>
        void UpsertCompletionCallback(ref Key key, ref Value value, ref Context ctx);

        /// <summary>
        /// RMW completion
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="ctx"></param>
        /// <param name="status"></param>
        void RMWCompletionCallback(ref Key key, ref Input input, ref Context ctx, Status status);

        /// <summary>
        /// Delete completion
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ctx"></param>
        void DeleteCompletionCallback(ref Key key, ref Context ctx);

        /// <summary>
        /// Checkpoint completion callback (called per client session)
        /// </summary>
        /// <param name="sessionId">Session ID reporting persistence</param>
        /// <param name="commitPoint">Commit point descriptor</param>
        void CheckpointCompletionCallback(string sessionId, CommitPoint commitPoint);

        /// <summary>
        /// Initial update for RMW
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="value"></param>
        /// <param name="ctx"></param>
        void InitialUpdater(ref Key key, ref Input input, ref Value value, ref Context ctx);

        /// <summary>
        /// Copy-update for RMW
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        /// <param name="ctx"></param>
        void CopyUpdater(ref Key key, ref Input input, ref Value oldValue, ref Value newValue, ref Context ctx);

        /// <summary>
        /// In-place update for RMW
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="value"></param>
        /// <param name="ctx"></param>
        bool InPlaceUpdater(ref Key key, ref Input input, ref Value value, ref Context ctx);

        /// <summary>
        /// Single reader
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="value"></param>
        /// <param name="dst"></param>
        /// <param name="ctx"></param>
        void SingleReader(ref Key key, ref Input input, ref Value value, ref Output dst, ref Context ctx);

        /// <summary>
        /// Concurrent reader
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="value"></param>
        /// <param name="dst"></param>
        /// <param name="ctx"></param>
        void ConcurrentReader(ref Key key, ref Input input, ref Value value, ref Output dst, ref Context ctx);

        /// <summary>
        /// Single writer
        /// </summary>
        /// <param name="key"></param>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="ctx"></param>
        void SingleWriter(ref Key key, ref Value src, ref Value dst, ref Context ctx);

        /// <summary>
        /// Concurrent writer
        /// </summary>
        /// <param name="key"></param>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="ctx"></param>
        bool ConcurrentWriter(ref Key key, ref Value src, ref Value dst, ref Context ctx);
    }

    /// <summary>
    /// Callback functions to FASTER (two-param version)
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public interface IFunctions<Key, Value> : IFunctions<Key, Value, Value, Value, Empty>
    {
    }

    /// <summary>
    /// Callback functions to FASTER (two-param version with context)
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    /// <typeparam name="Context"></typeparam>
    public interface IFunctions<Key, Value, Context> : IFunctions<Key, Value, Value, Value, Context>
    {
    }
}