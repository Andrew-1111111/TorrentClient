using System.Drawing;
using System.Windows.Forms;

namespace TorrentClient
{
    /// <summary>
    /// Диалог подтверждения удаления торрента с опцией удаления файлов
    /// </summary>
    public partial class RemoveTorrentDialog : Form
    {
        /// <summary>
        /// Удалить загруженные файлы
        /// </summary>
        public bool DeleteFiles => _deleteFilesCheckBox.Checked;

        public RemoveTorrentDialog(int torrentCount = 1)
        {
            InitializeComponent();
            
            // Обновляем текст в зависимости от количества торрентов
            if (torrentCount > 1)
            {
                Text = $"Удаление торрентов ({torrentCount})";
                _messageLabel.Text = $"Вы уверены, что хотите удалить {torrentCount} выбранных торрентов?";
            }
        }
    }
}
