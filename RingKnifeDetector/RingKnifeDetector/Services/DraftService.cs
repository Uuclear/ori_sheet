using System;
using System.IO;
using System.Text.Json;
using RingKnifeDetector.Models;

namespace RingKnifeDetector.Services
{
    /// <summary>
    /// 草稿管理服务
    /// </summary>
    public class DraftService
    {
        private readonly string _draftDirectory;

        public DraftService(string? draftDirectory = null)
        {
            _draftDirectory = draftDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RingKnifeDetector",
                "Drafts");

            // 确保目录存在
            if (!Directory.Exists(_draftDirectory))
            {
                Directory.CreateDirectory(_draftDirectory);
            }
        }

        /// <summary>
        /// 保存草稿
        /// </summary>
        /// <param name="entrustNo">委托编号</param>
        /// <param name="draft">草稿数据</param>
        /// <returns>保存结果</returns>
        public DraftSaveResponse SaveDraft(string entrustNo, DraftSaveRequest draft)
        {
            try
            {
                if (string.IsNullOrEmpty(entrustNo))
                {
                    return new DraftSaveResponse
                    {
                        Success = false,
                        Message = "委托编号不能为空"
                    };
                }

                var filePath = GetDraftFilePath(entrustNo);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(draft, options);
                File.WriteAllText(filePath, json);

                return new DraftSaveResponse
                {
                    Success = true,
                    Message = "草稿保存成功",
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                return new DraftSaveResponse
                {
                    Success = false,
                    Message = $"保存草稿失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 加载草稿
        /// </summary>
        /// <param name="entrustNo">委托编号</param>
        /// <returns>加载结果</returns>
        public DraftLoadResponse LoadDraft(string entrustNo)
        {
            try
            {
                if (string.IsNullOrEmpty(entrustNo))
                {
                    return new DraftLoadResponse
                    {
                        Success = false,
                        Message = "委托编号不能为空"
                    };
                }

                var filePath = GetDraftFilePath(entrustNo);
                if (!File.Exists(filePath))
                {
                    return new DraftLoadResponse
                    {
                        Success = true,
                        Message = "未找到草稿",
                        Draft = null
                    };
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var draft = JsonSerializer.Deserialize<DraftSaveRequest>(json, options);
                var fileInfo = new FileInfo(filePath);

                return new DraftLoadResponse
                {
                    Success = true,
                    Message = "草稿加载成功",
                    Draft = draft,
                    UpdatedAt = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                return new DraftLoadResponse
                {
                    Success = false,
                    Message = $"加载草稿失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 删除草稿
        /// </summary>
        /// <param name="entrustNo">委托编号</param>
        /// <returns>删除结果</returns>
        public DraftSaveResponse DeleteDraft(string entrustNo)
        {
            try
            {
                if (string.IsNullOrEmpty(entrustNo))
                {
                    return new DraftSaveResponse
                    {
                        Success = false,
                        Message = "委托编号不能为空"
                    };
                }

                var filePath = GetDraftFilePath(entrustNo);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return new DraftSaveResponse
                {
                    Success = true,
                    Message = "草稿删除成功"
                };
            }
            catch (Exception ex)
            {
                return new DraftSaveResponse
                {
                    Success = false,
                    Message = $"删除草稿失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取草稿文件路径
        /// </summary>
        private string GetDraftFilePath(string entrustNo)
        {
            // 清理文件名中的非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedNo = new string(entrustNo.Where(c => !invalidChars.Contains(c)).ToArray());
            return Path.Combine(_draftDirectory, $"{sanitizedNo}.json");
        }

        /// <summary>
        /// 获取所有草稿列表
        /// </summary>
        /// <returns>草稿列表</returns>
        public List<DraftInfo> GetDraftList()
        {
            var drafts = new List<DraftInfo>();

            try
            {
                if (!Directory.Exists(_draftDirectory))
                {
                    return drafts;
                }

                var files = Directory.GetFiles(_draftDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var entrustNo = Path.GetFileNameWithoutExtension(file);

                        var json = File.ReadAllText(file);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };

                        var draft = JsonSerializer.Deserialize<DraftSaveRequest>(json, options);

                        drafts.Add(new DraftInfo
                        {
                            EntrustNo = entrustNo,
                            ProjectName = draft?.Project?.ProjectName ?? string.Empty,
                            UpdatedAt = fileInfo.LastWriteTime,
                            FilePath = file
                        });
                    }
                    catch
                    {
                        // 跳过无效的草稿文件
                    }
                }
            }
            catch
            {
                // 忽略目录访问错误
            }

            return drafts.OrderByDescending(d => d.UpdatedAt).ToList();
        }
    }

    /// <summary>
    /// 草稿信息
    /// </summary>
    public class DraftInfo
    {
        public string EntrustNo { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}