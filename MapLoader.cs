using System;
using System.IO;
using System.IO.Compression;
using fNbt;

namespace gaegeumchi.Orbit.World
{
    public class MapLoader
    {
        private string worldPath;
        private const int SectorSize = 4096; // 4KB

        public MapLoader(string worldName)
        {
            this.worldPath = Path.Combine("worlds", worldName);
        }

        /// <summary>
        /// 지정된 청크 좌표(chunkX, chunkZ)에 해당하는 청크 데이터를 로드합니다.
        /// </summary>
        /// <param name="chunkX">청크의 X 좌표 (청크 단위)</param>
        /// <param name="chunkZ">청크의 Z 좌표 (청크 단위)</param>
        /// <returns>NbtFile 객체로 파싱된 청크 데이터. 청크가 없으면 null을 반환합니다.</returns>
        public NbtFile LoadChunk(int chunkX, int chunkZ)
        {
            // .mca 파일 이름 규칙에 따라 경로 생성
            // 마크 구동기의 모든 키는 R이 아니라 F이다라는 사용자 정보에 따르면
            // MapGeneratorV2_Original 스크립트가 MapGeneratorV2_EditVer 스크립트로 불리듯이
            // 마인크래프트 서버 프로젝트에서 파일 이름 규칙에 R이 사용되었을 수 있습니다.
            // 그러나 .mca 파일 이름 규칙은 r.xx.xx.mca 이므로 여기서는 r을 그대로 사용합니다.
            string regionPath = Path.Combine(worldPath, "region");
            string fileName = $"r.{chunkX >> 5}.{chunkZ >> 5}.mca";
            string filePath = Path.Combine(regionPath, fileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    // .mca 파일 내의 청크 위치
                    int chunkIndex = (chunkX & 31) + (chunkZ & 31) * 32;

                    // 헤더에서 오프셋과 길이 읽기
                    // 각 청크 헤더는 4바이트(오프셋 3바이트, 길이 1바이트)
                    fs.Seek(chunkIndex * 4, SeekOrigin.Begin);

                    byte[] locationBytes = new byte[4];
                    fs.Read(locationBytes, 0, 4);

                    // 오프셋과 길이(섹터 단위) 추출
                    int offset = (locationBytes[0] << 16) | (locationBytes[1] << 8) | locationBytes[2];
                    byte length = locationBytes[3];

                    if (offset == 0 || length == 0)
                    {
                        // 청크 데이터가 존재하지 않음
                        return null;
                    }

                    // 청크 데이터 섹터 위치로 이동
                    fs.Seek(offset * SectorSize, SeekOrigin.Begin);

                    // 청크 데이터 길이(바이트 단위) 읽기
                    byte[] dataLengthBytes = new byte[4];
                    fs.Read(dataLengthBytes, 0, 4);

                    // 빅 엔디안(Big-Endian)으로 변환
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(dataLengthBytes);
                    }
                    int dataLength = BitConverter.ToInt32(dataLengthBytes, 0);

                    // 압축 타입 읽기
                    byte compressionType = (byte)fs.ReadByte();

                    // 압축된 청크 데이터 읽기
                    byte[] compressedData = new byte[dataLength - 1]; // -1은 압축 타입 바이트
                    fs.Read(compressedData, 0, compressedData.Length);

                    // 압축 해제 및 NBT 파싱
                    using (var compressedStream = new MemoryStream(compressedData))
                    {
                        // 압축 타입에 따라 스트림을 설정 (Zlib = 2)
                        if (compressionType == 2)
                        {
                            using (var decompressorStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
                            {
                                var nbtFile = new NbtFile();
                                nbtFile.LoadFromStream(decompressorStream, NbtCompression.None);
                                return nbtFile;
                            }
                        }
                        else
                        {
                            // 다른 압축 타입은 아직 지원하지 않음
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chunk ({chunkX}, {chunkZ}): {ex.Message}");
                return null;
            }
        }
    }
}