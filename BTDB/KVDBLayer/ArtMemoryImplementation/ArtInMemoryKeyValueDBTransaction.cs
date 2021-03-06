using BTDB.ARTLib;
using BTDB.Buffer;
using System;
using System.Collections.Generic;

namespace BTDB.KVDBLayer
{
    class ArtInMemoryKeyValueDBTransaction : IKeyValueDBTransaction
    {
        readonly ArtInMemoryKeyValueDB _keyValueDB;
        internal IRootNode ArtRoot { get; private set; }
        ICursor _cursor;
        ICursor _cursor2;
        byte[] _prefix;
        bool _readOnly;
        bool _writing;
        bool _preapprovedWriting;
        long _prefixKeyStart;
        long _prefixKeyCount;
        long _keyIndex;

        public ArtInMemoryKeyValueDBTransaction(ArtInMemoryKeyValueDB keyValueDB, IRootNode artRoot, bool writing, bool readOnly)
        {
            _preapprovedWriting = writing;
            _readOnly = readOnly;
            _keyValueDB = keyValueDB;
            _prefix = Array.Empty<byte>();
            _prefixKeyStart = 0;
            _prefixKeyCount = -1;
            _keyIndex = -1;
            _cursor = artRoot.CreateCursor();
            _cursor2 = null;
            ArtRoot = artRoot;
        }

        ~ArtInMemoryKeyValueDBTransaction()
        {
            if (ArtRoot != null)
            {
                _keyValueDB.Logger?.ReportTransactionLeak(this);
                Dispose();
            }
        }

        public void SetKeyPrefix(ByteBuffer prefix)
        {
            _prefix = prefix.ToByteArray();
            _prefixKeyStart = _prefix.Length == 0 ? 0 : -1;
            _prefixKeyCount = -1;
            InvalidateCurrentKey();
        }

        public bool FindFirstKey()
        {
            return SetKeyIndex(0);
        }

        public bool FindLastKey()
        {
            var count = GetKeyValueCount();
            if (count <= 0) return false;
            return SetKeyIndex(count - 1);
        }

        public bool FindPreviousKey()
        {
            if (!_cursor.IsValid()) return FindLastKey();
            if (_cursor.MovePrevious())
            {
                if (_cursor.KeyHasPrefix(_prefix))
                {
                    if (_keyIndex >= 0)
                        _keyIndex--;
                    return true;
                }
            }
            InvalidateCurrentKey();
            return false;
        }

        public bool FindNextKey()
        {
            if (!_cursor.IsValid()) return FindFirstKey();
            if (_cursor.MoveNext())
            {
                if (_cursor.KeyHasPrefix(_prefix))
                {
                    if (_keyIndex >= 0)
                        _keyIndex++;
                    return true;
                }
            }
            InvalidateCurrentKey();
            return false;
        }

        public FindResult Find(ByteBuffer key)
        {
            var result = _cursor.Find(_prefix, key.AsSyncReadOnlySpan());
            _keyIndex = -1;
            return result;
        }

        public bool CreateOrUpdateKeyValue(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            MakeWritable();
            bool result;
            var keyLen = _prefix.Length + key.Length;
            if (_prefix.Length == 0)
            {
                result = _cursor.Upsert(key, value);
            }
            else if (key.Length == 0)
            {
                result = _cursor.Upsert(_prefix, value);
            }
            else
            {
                Span<byte> temp = keyLen < 256 ? stackalloc byte[keyLen] : new byte[keyLen];
                _prefix.CopyTo(temp);
                key.CopyTo(temp.Slice(_prefix.Length));
                result = _cursor.Upsert(_prefix, value);
            }
            _keyIndex = -1;
            if (result && _prefixKeyCount >= 0) _prefixKeyCount++;
            return result;
        }

        public bool CreateOrUpdateKeyValue(ByteBuffer key, ByteBuffer value)
        {
            return CreateOrUpdateKeyValue(key.AsSyncReadOnlySpan(), value.AsSyncReadOnlySpan());
        }

        void MakeWritable()
        {
            if (_writing) return;
            if (_preapprovedWriting)
            {
                _writing = true;
                _preapprovedWriting = false;
                return;
            }
            if (_readOnly)
            {
                throw new BTDBTransactionRetryException("Cannot write from readOnly transaction");
            }
            var oldArtRoot = ArtRoot;
            ArtRoot = _keyValueDB.MakeWritableTransaction(this, oldArtRoot);
            _cursor.SetNewRoot(ArtRoot);
            _cursor2?.SetNewRoot(ArtRoot);
            ArtRoot.DescriptionForLeaks = _descriptionForLeaks;
            _writing = true;
        }

        public long GetKeyValueCount()
        {
            if (_prefixKeyCount >= 0) return _prefixKeyCount;
            if (_prefix.Length == 0)
            {
                _prefixKeyCount = ArtRoot.GetCount();
                return _prefixKeyCount;
            }
            CalcPrefixKeyStart();
            if (_prefixKeyStart < 0)
            {
                _prefixKeyCount = 0;
                return 0;
            }
            if (_cursor2 == null)
            {
                _cursor2 = ArtRoot.CreateCursor();
            }
            _cursor2.FindLast(_prefix);
            _prefixKeyCount = _cursor2.CalcIndex() - _prefixKeyStart + 1;
            return _prefixKeyCount;
        }

        public long GetKeyIndex()
        {
            if (_keyIndex < 0)
            {
                if (!_cursor.IsValid())
                    return -1;
                _keyIndex = _cursor.CalcIndex();
            }
            CalcPrefixKeyStart();
            return _keyIndex - _prefixKeyStart;
        }

        void CalcPrefixKeyStart()
        {
            if (_prefixKeyStart >= 0) return;
            if (_cursor2 == null)
            {
                _cursor2 = ArtRoot.CreateCursor();
            }
            if (_cursor2.FindFirst(_prefix))
            {
                _prefixKeyStart = _cursor2.CalcIndex();
            }
            else
            {
                _prefixKeyStart = -1;
            }
        }

        public bool SetKeyIndex(long index)
        {
            CalcPrefixKeyStart();
            if (_prefixKeyStart < 0)
            {
                InvalidateCurrentKey();
                return false;
            }
            _keyIndex = index + _prefixKeyStart;
            if (!_cursor.SeekIndex(_keyIndex))
            {
                InvalidateCurrentKey();
                return false;
            }
            if (_cursor.KeyHasPrefix(_prefix))
            {
                return true;
            }
            InvalidateCurrentKey();
            return false;
        }

        ByteBuffer GetCurrentKeyFromStack()
        {
            var result = ByteBuffer.NewAsync(new byte[_cursor.GetKeyLength()]);
            _cursor.FillByKey(result.AsSyncSpan());
            return result;
        }

        public void InvalidateCurrentKey()
        {
            _keyIndex = -1;
            _cursor.Invalidate();
        }

        public bool IsValidKey()
        {
            return _cursor.IsValid();
        }

        public ByteBuffer GetKey()
        {
            if (!IsValidKey()) return ByteBuffer.NewEmpty();
            var wholeKey = GetCurrentKeyFromStack();
            return ByteBuffer.NewAsync(wholeKey.Buffer, wholeKey.Offset + _prefix.Length, wholeKey.Length - _prefix.Length);
        }

        public ByteBuffer GetKeyIncludingPrefix()
        {
            if (!IsValidKey()) return ByteBuffer.NewEmpty();
            return GetCurrentKeyFromStack();
        }

        public ByteBuffer GetValue()
        {
            if (!IsValidKey()) return ByteBuffer.NewEmpty();
            return ByteBuffer.NewAsync(_cursor.GetValue());
        }

        public ReadOnlySpan<byte> GetValueAsReadOnlySpan()
        {
            if (!IsValidKey()) return new ReadOnlySpan<byte>();
            return _cursor.GetValue();
        }

        void EnsureValidKey()
        {
            if (!_cursor.IsValid())
            {
                throw new InvalidOperationException("Current key is not valid");
            }
        }

        public void SetValue(ByteBuffer value)
        {
            EnsureValidKey();
            MakeWritable();
            _cursor.WriteValue(value.AsSyncReadOnlySpan());
        }

        public void EraseCurrent()
        {
            EnsureValidKey();
            MakeWritable();
            _cursor.Erase();
            InvalidateCurrentKey();
            if (_prefixKeyCount >= 0) _prefixKeyCount--;
        }

        public void EraseAll()
        {
            EraseRange(0, long.MaxValue);
        }

        public void EraseRange(long firstKeyIndex, long lastKeyIndex)
        {
            if (firstKeyIndex < 0) firstKeyIndex = 0;
            if (lastKeyIndex >= GetKeyValueCount()) lastKeyIndex = _prefixKeyCount - 1;
            if (lastKeyIndex < firstKeyIndex) return;
            MakeWritable();
            firstKeyIndex += _prefixKeyStart;
            lastKeyIndex += _prefixKeyStart;
            if (_cursor2 == null)
            {
                _cursor2 = ArtRoot.CreateCursor();
            }
            _cursor.SeekIndex(firstKeyIndex);
            _cursor2.SeekIndex(lastKeyIndex);
            _cursor.EraseTo(_cursor2);
            InvalidateCurrentKey();
            _prefixKeyCount -= lastKeyIndex - firstKeyIndex + 1;
        }

        public bool IsWriting()
        {
            return _writing || _preapprovedWriting;
        }

        public bool IsReadOnly()
        {
            return _readOnly;
        }

        public ulong GetCommitUlong()
        {
            return ArtRoot.CommitUlong;
        }

        public void SetCommitUlong(ulong value)
        {
            if (ArtRoot.CommitUlong != value)
            {
                MakeWritable();
                ArtRoot.CommitUlong = value;
            }
        }

        public void NextCommitTemporaryCloseTransactionLog()
        {
            // There is no transaction log ...
        }

        public void Commit()
        {
            if (ArtRoot == null) throw new BTDBException("Transaction already commited or disposed");
            InvalidateCurrentKey();
            var currentArtRoot = ArtRoot;
            ArtRoot = null;
            if (_preapprovedWriting)
            {
                _preapprovedWriting = false;
                _keyValueDB.RevertWritingTransaction(currentArtRoot);
            }
            else if (_writing)
            {
                _keyValueDB.CommitWritingTransaction(currentArtRoot);
                _writing = false;
            }
            else
            {
                _keyValueDB.DereferenceRoot(currentArtRoot);
            }
        }

        public void Dispose()
        {
            var currentArtRoot = ArtRoot;
            ArtRoot = null;
            if (_writing || _preapprovedWriting)
            {
                _keyValueDB.RevertWritingTransaction(currentArtRoot);
                _writing = false;
                _preapprovedWriting = false;
            }
            else if (currentArtRoot != null)
            {
                _keyValueDB.DereferenceRoot(currentArtRoot);
            }
            GC.SuppressFinalize(this);
        }

        public long GetTransactionNumber()
        {
            return ArtRoot.TransactionId;
        }

        public KeyValuePair<uint, uint> GetStorageSizeOfCurrentKey()
        {
            return new KeyValuePair<uint, uint>((uint)_cursor.GetKeyLength(), (uint)_cursor.GetValueLength());
        }

        public byte[] GetKeyPrefix()
        {
            return _prefix;
        }

        public ulong GetUlong(uint idx)
        {
            return ArtRoot.GetUlong(idx);
        }

        public void SetUlong(uint idx, ulong value)
        {
            if (ArtRoot.GetUlong(idx) != value)
            {
                MakeWritable();
                ArtRoot.SetUlong(idx, value);
            }
        }

        public uint GetUlongCount()
        {
            return ArtRoot.GetUlongCount();
        }

        string _descriptionForLeaks;

        public string DescriptionForLeaks
        {
            get { return _descriptionForLeaks; }
            set
            {
                _descriptionForLeaks = value;
                if (_preapprovedWriting || _writing) ArtRoot.DescriptionForLeaks = value;
            }
        }

        public IKeyValueDB Owner => _keyValueDB;

        public bool RollbackAdvised { get; set; }
    }
}
