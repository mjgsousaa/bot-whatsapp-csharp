using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using BotWhatsappCSharp.Models;

namespace BotWhatsappCSharp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string baseDir = Path.Combine(appData, "BotZapAI");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            
            _dbPath = Path.Combine(baseDir, "botzap.db");
            
            InicializarBanco();
            MigrarDadosLegados();
        }

        private void InicializarBanco()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                string sql = @"
                    CREATE TABLE IF NOT EXISTS agendamentos (
                        id TEXT PRIMARY KEY,
                        cliente_nome TEXT NOT NULL,
                        cliente_telefone TEXT NOT NULL,
                        data_hora TEXT NOT NULL,
                        servico TEXT DEFAULT 'Consulta',
                        status TEXT DEFAULT 'Confirmado',
                        criado_em TEXT DEFAULT (datetime('now','localtime'))
                    );

                    CREATE TABLE IF NOT EXISTS gatilhos (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        comando TEXT NOT NULL UNIQUE,
                        resposta TEXT NOT NULL,
                        caminhos_arquivos TEXT DEFAULT ''
                    );

                    CREATE TABLE IF NOT EXISTS historico_conversas (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        numero_telefone TEXT NOT NULL,
                        role TEXT NOT NULL,
                        conteudo TEXT NOT NULL,
                        criado_em TEXT DEFAULT (datetime('now','localtime'))
                    );

                    CREATE TABLE IF NOT EXISTS configuracoes (
                        chave TEXT PRIMARY KEY,
                        valor TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS slots_disponiveis (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        dia_semana INTEGER NOT NULL,
                        hora_inicio TEXT NOT NULL,
                        hora_fim TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS slots_configurados (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        data_hora TEXT NOT NULL UNIQUE,
                        disponivel INTEGER DEFAULT 1,
                        agendado_por TEXT DEFAULT '',
                        telefone_cliente TEXT DEFAULT ''
                    );";

                using var command = new SqliteCommand(sql, connection);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao inicializar banco: {ex.Message}");
            }
        }

        private void MigrarDadosLegados()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string baseDir = Path.Combine(appData, "BotZapAI");

            // Migra settings.json
            string settingsPath = Path.Combine(baseDir, "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<SettingsModel>(json);
                    if (settings != null) SalvarSettings(settings);
                    File.Move(settingsPath, settingsPath + ".migrado");
                }
                catch { }
            }

            // Migra triggers.json
            string triggersPath = Path.Combine(baseDir, "triggers.json");
            if (File.Exists(triggersPath))
            {
                try
                {
                    string json = File.ReadAllText(triggersPath);
                    var list = JsonSerializer.Deserialize<List<GatilhoModel>>(json);
                    if (list != null) foreach (var g in list) SalvarGatilho(g);
                    File.Move(triggersPath, triggersPath + ".migrado");
                }
                catch { }
            }

            // Migra agendamentos.json
            string agendPath = Path.Combine(baseDir, "agendamentos.json");
            if (File.Exists(agendPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(agendPath));
                    if (doc.RootElement.TryGetProperty("Agendamentos", out var ags))
                    {
                        var list = JsonSerializer.Deserialize<List<AgendamentoModel>>(ags.GetRawText());
                        if (list != null) 
                            foreach (var a in list) SalvarAgendamento(a);
                    }
                    File.Move(agendPath, agendPath + ".migrado");
                }
                catch { }
            }
            
            SalvarSlotsPadrao();
        }

        // --- AGENDAMENTOS ---
        public List<AgendamentoModel> ListarAgendamentos()
        {
            var list = new List<AgendamentoModel>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT * FROM agendamentos ORDER BY data_hora ASC";
                using var command = new SqliteCommand(sql, connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AgendamentoModel
                    {
                        Id = reader.GetString(0),
                        ClienteNome = reader.GetString(1),
                        ClienteTelefone = reader.GetString(2),
                        DataHora = DateTime.Parse(reader.GetString(3)),
                        Servico = reader.IsDBNull(4) ? "Consulta" : reader.GetString(4),
                        Status = reader.IsDBNull(5) ? "Confirmado" : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex) { Logger.Log($"Erro ListarAgendamentos: {ex.Message}"); }
            return list;
        }

        public bool SalvarAgendamento(AgendamentoModel ag)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = @"INSERT OR REPLACE INTO agendamentos 
                               (id, cliente_nome, cliente_telefone, data_hora, servico, status) 
                               VALUES (@id, @nome, @tel, @dt, @svc, @st)";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@id", ag.Id);
                cmd.Parameters.AddWithValue("@nome", ag.ClienteNome);
                cmd.Parameters.AddWithValue("@tel", ag.ClienteTelefone);
                cmd.Parameters.AddWithValue("@dt", ag.DataHora.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@svc", ag.Servico);
                cmd.Parameters.AddWithValue("@st", ag.Status);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro SalvarAgendamento: {ex.Message}"); return false; }
        }

        public bool AtualizarStatusAgendamento(string id, string novoStatus)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "UPDATE agendamentos SET status = @st WHERE id = @id";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@st", novoStatus);
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro AtualizarStatusAgendamento: {ex.Message}"); return false; }
        }

        public bool ExisteConflito(DateTime dataHora)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT COUNT(*) FROM agendamentos WHERE data_hora = @dt AND status = 'Confirmado'";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@dt", dataHora.ToString("yyyy-MM-dd HH:mm:ss"));
                object? result = cmd.ExecuteScalar();
                return result != null && Convert.ToInt32(result) > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro ExisteConflito: {ex.Message}"); return true; }
        }

        // --- GATILHOS ---
        public List<GatilhoModel> ListarGatilhos()
        {
            var list = new List<GatilhoModel>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT comando, resposta, caminhos_arquivos FROM gatilhos";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string arquivosStr = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    list.Add(new GatilhoModel
                    {
                        Comando = reader.GetString(0),
                        Resposta = reader.GetString(1),
                        CaminhosArquivos = arquivosStr.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                    });
                }
            }
            catch (Exception ex) { Logger.Log($"Erro ListarGatilhos: {ex.Message}"); }
            return list;
        }

        public bool SalvarGatilho(GatilhoModel g)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "INSERT OR REPLACE INTO gatilhos (comando, resposta, caminhos_arquivos) VALUES (@cmd, @resp, @files)";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@cmd", g.Comando);
                cmd.Parameters.AddWithValue("@resp", g.Resposta);
                cmd.Parameters.AddWithValue("@files", string.Join("|", g.CaminhosArquivos ?? new List<string>()));
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro SalvarGatilho: {ex.Message}"); return false; }
        }

        public bool RemoverGatilho(string comando)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "DELETE FROM gatilhos WHERE comando = @cmd";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@cmd", comando);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro RemoverGatilho: {ex.Message}"); return false; }
        }

        // --- HISTÓRICO ---
        public List<(string Role, string Content)> CarregarHistorico(string telefone)
        {
            var list = new List<(string Role, string Content)>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT role, conteudo FROM historico_conversas WHERE numero_telefone = @tel ORDER BY id ASC LIMIT 20";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@tel", telefone);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
            catch (Exception ex) { Logger.Log($"Erro CarregarHistorico: {ex.Message}"); }
            return list;
        }

        public void SalvarMensagem(string telefone, string role, string conteudo)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sqlInsert = "INSERT INTO historico_conversas (numero_telefone, role, conteudo) VALUES (@tel, @role, @cont)";
                using var cmdInsert = new SqliteCommand(sqlInsert, connection);
                cmdInsert.Parameters.AddWithValue("@tel", telefone);
                cmdInsert.Parameters.AddWithValue("@role", role);
                cmdInsert.Parameters.AddWithValue("@cont", conteudo);
                cmdInsert.ExecuteNonQuery();

                string sqlDelete = @"DELETE FROM historico_conversas WHERE numero_telefone = @tel 
                                     AND id NOT IN (SELECT id FROM historico_conversas WHERE numero_telefone = @tel ORDER BY id DESC LIMIT 40)";
                using var cmdDel = new SqliteCommand(sqlDelete, connection);
                cmdDel.Parameters.AddWithValue("@tel", telefone);
                cmdDel.ExecuteNonQuery();
            }
            catch (Exception ex) { Logger.Log($"Erro SalvarMensagem: {ex.Message}"); }
        }

        public void LimparHistorico(string telefone)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "DELETE FROM historico_conversas WHERE numero_telefone = @tel";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@tel", telefone);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Logger.Log($"Erro LimparHistorico: {ex.Message}"); }
        }

        // --- CONFIGURAÇÕES ---
        public string ObterConfiguracao(string chave, string valorPadrao = "")
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT valor FROM configuracoes WHERE chave = @ch";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ch", chave);
                var obj = cmd.ExecuteScalar();
                return obj?.ToString() ?? valorPadrao;
            }
            catch { return valorPadrao; }
        }

        public void SalvarConfiguracao(string chave, string? valor)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "INSERT OR REPLACE INTO configuracoes (chave, valor) VALUES (@ch, @val)";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ch", chave);
                cmd.Parameters.AddWithValue("@val", valor ?? "");
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Logger.Log($"Erro SalvarConfiguracao: {ex.Message}"); }
        }

        public SettingsModel CarregarSettings()
        {
            return new SettingsModel
            {
                ApiKey = ObterConfiguracao("api_key"),
                SystemPrompt = ObterConfiguracao("system_prompt", "Você é um assistente virtual útil e educado."),
                AiMediaFile = ObterConfiguracao("ai_media_file"),
                BulkMediaFile = ObterConfiguracao("bulk_media_file")
            };
        }

        public void SalvarSettings(SettingsModel s)
        {
            SalvarConfiguracao("api_key", s.ApiKey);
            SalvarConfiguracao("system_prompt", s.SystemPrompt);
            SalvarConfiguracao("ai_media_file", s.AiMediaFile);
            SalvarConfiguracao("bulk_media_file", s.BulkMediaFile);
        }

        // --- SLOTS ---
        public List<SlotDisponivel> ListarSlots()
        {
            var list = new List<SlotDisponivel>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = "SELECT dia_semana, hora_inicio, hora_fim FROM slots_disponiveis";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new SlotDisponivel
                    {
                        DiaSemana = (DayOfWeek)reader.GetInt32(0),
                        HoraInicio = TimeSpan.Parse(reader.GetString(1)),
                        HoraFim = TimeSpan.Parse(reader.GetString(2))
                    });
                }
            }
            catch (Exception ex) { Logger.Log($"Erro ListarSlots: {ex.Message}"); }
            return list;
        }

        // --- SLOTS CONFIGURADOS (CALENDÁRIO) ---
        public List<SlotConfigurado> ListarSlotsMes(int ano, int mes)
        {
            var list = new List<SlotConfigurado>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                // strftime '%m' retorna com zero à esquerda, então precisamos formatar o mês
                string mesPad = mes.ToString("D2");
                string sql = "SELECT data_hora, disponivel, agendado_por, telefone_cliente FROM slots_configurados " +
                             "WHERE strftime('%Y', data_hora) = @ano AND strftime('%m', data_hora) = @mes " +
                             "ORDER BY data_hora ASC";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ano", ano.ToString());
                cmd.Parameters.AddWithValue("@mes", mesPad);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new SlotConfigurado
                    {
                        DataHora = DateTime.Parse(reader.GetString(0)),
                        Disponivel = reader.GetInt32(1) == 1,
                        AgendadoPor = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        TelefoneCliente = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex) { Logger.Log($"Erro ListarSlotsMes: {ex.Message}"); }
            return list;
        }

        public void ToggleSlot(DateTime dataHora)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string dtStr = dataHora.ToString("yyyy-MM-dd HH:mm:ss");
                
                string checkSql = "SELECT COUNT(*) FROM slots_configurados WHERE data_hora = @dt";
                using var cmdCheck = new SqliteCommand(checkSql, connection);
                cmdCheck.Parameters.AddWithValue("@dt", dtStr);
                long count = (long)cmdCheck.ExecuteScalar()!;

                if (count > 0)
                {
                    string updateSql = "UPDATE slots_configurados SET disponivel = 1 - disponivel WHERE data_hora = @dt";
                    using var cmdUp = new SqliteCommand(updateSql, connection);
                    cmdUp.Parameters.AddWithValue("@dt", dtStr);
                    cmdUp.ExecuteNonQuery();
                }
                else
                {
                    string insertSql = "INSERT INTO slots_configurados (data_hora, disponivel) VALUES (@dt, 1)";
                    using var cmdIns = new SqliteCommand(insertSql, connection);
                    cmdIns.Parameters.AddWithValue("@dt", dtStr);
                    cmdIns.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Logger.Log($"Erro ToggleSlot: {ex.Message}"); }
        }

        public bool AgendarSlot(DateTime dataHora, string nome, string telefone)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string sql = @"UPDATE slots_configurados 
                               SET disponivel = 0, agendado_por = @nome, telefone_cliente = @tel 
                               WHERE data_hora = @dt AND disponivel = 1";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@nome", nome);
                cmd.Parameters.AddWithValue("@tel", telefone);
                cmd.Parameters.AddWithValue("@dt", dataHora.ToString("yyyy-MM-dd HH:mm:ss"));
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex) { Logger.Log($"Erro AgendarSlot: {ex.Message}"); return false; }
        }

        public List<DateTime> GetHorariosDisponiveisMes(int ano, int mes)
        {
            var list = new List<DateTime>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string mesPad = mes.ToString("D2");
                string sql = @"SELECT data_hora FROM slots_configurados 
                               WHERE disponivel = 1 AND data_hora > datetime('now','localtime') 
                               AND strftime('%Y', data_hora) = @ano AND strftime('%m', data_hora) = @mes 
                               ORDER BY data_hora ASC";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ano", ano.ToString());
                cmd.Parameters.AddWithValue("@mes", mesPad);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(DateTime.Parse(reader.GetString(0)));
                }
            }
            catch (Exception ex) { Logger.Log($"Erro GetHorariosDisponiveisMes: {ex.Message}"); }
            return list;
        }

        public void SalvarSlotsPadrao()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                string checkSql = "SELECT COUNT(*) FROM slots_disponiveis";
                using var cmdCheck = new SqliteCommand(checkSql, connection);
                object? result = cmdCheck.ExecuteScalar();
                if (result != null && Convert.ToInt32(result) == 0)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        string sql = "INSERT INTO slots_disponiveis (dia_semana, hora_inicio, hora_fim) VALUES (@dia, '09:00', '17:00')";
                        using var cmd = new SqliteCommand(sql, connection);
                        cmd.Parameters.AddWithValue("@dia", i);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }
    }
}
