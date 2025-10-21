import { ApiClient, AnalysisResult } from './api-client';
import './styles.css';

export interface FinDocWidgetConfig {
    apiUrl: string;
    clientId?: string;
    onSuccess?: (data: AnalysisResult) => void;
    onError?: (error: string) => void;
    onProgress?: (fileName: string, progress: number, message: string) => void;
}

interface FileStatus {
    file: File;
    status: 'pending' | 'uploading' | 'processing' | 'completed' | 'error';
    progress: number;
    message: string;
    analysisId?: string;
    result?: AnalysisResult;
    error?: string;
}

class FinDocUploadWidget extends HTMLElement {
    private config: FinDocWidgetConfig | null = null;
    private shadow: ShadowRoot;
    private fileStatuses: Map<string, FileStatus> = new Map();
    private apiClient: ApiClient | null = null;

    constructor() {
        super();
        this.shadow = this.attachShadow({ mode: 'open' });
    }

    connectedCallback() {
        this.render();
        this.attachEventListeners();
    }

    configure(config: FinDocWidgetConfig) {
        this.config = config;
        this.apiClient = new ApiClient(config.apiUrl);
    }

    private render() {
        this.shadow.innerHTML = `
      <style>
        ${this.getStyles()}
      </style>
      <div class="findoc-widget">
        <div class="findoc-header">
          <div class="findoc-icon">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
              <polyline points="14 2 14 8 20 8"/>
            </svg>
          </div>
          <h2>Importar Carteira de Investimentos</h2>
          <p>Envie seus relatórios em PDF para análise automática</p>
        </div>

        <div class="findoc-upload-area" id="upload-area">
          <input 
            type="file" 
            id="file-input" 
            accept=".pdf" 
            multiple 
            style="display: none"
          />
          <label for="file-input" class="upload-label">
            <div class="upload-icon">📄</div>
            <div class="upload-text">
              <p><strong>Arraste PDFs aqui</strong> ou clique para selecionar</p>
              <small>Formatos aceitos: XP, Rico, BTG, Inter, Nubank, Modal</small>
            </div>
          </label>
        </div>

        <div class="findoc-files-list" id="files-list" style="display: none">
          <h3>Arquivos selecionados</h3>
          <div id="files-container"></div>
        </div>

        <div class="findoc-actions">
          <button id="cancel-btn" class="btn-secondary">Cancelar</button>
          <button id="upload-btn" class="btn-primary" disabled>
            Processar arquivos
          </button>
        </div>
      </div>
    `;
    }

    private getStyles(): string {
        return `
      .findoc-widget {
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
        max-width: 600px;
        margin: 0 auto;
        padding: 24px;
        background: #ffffff;
        border-radius: 12px;
        box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
      }
      .findoc-header {
        text-align: center;
        margin-bottom: 24px;
      }
      .findoc-icon {
        display: inline-block;
        color: #3b82f6;
        margin-bottom: 16px;
      }
      .findoc-header h2 {
        margin: 0 0 8px 0;
        font-size: 24px;
        font-weight: 600;
        color: #1f2937;
      }
      .findoc-header p {
        margin: 0;
        color: #6b7280;
        font-size: 14px;
      }
      .findoc-upload-area {
        border: 2px dashed #d1d5db;
        border-radius: 8px;
        padding: 40px 20px;
        text-align: center;
        cursor: pointer;
        transition: all 0.3s ease;
        margin-bottom: 20px;
        background: #f9fafb;
      }
      .findoc-upload-area:hover {
        border-color: #3b82f6;
        background: #eff6ff;
      }
      .findoc-upload-area.dragover {
        border-color: #3b82f6;
        background: #dbeafe;
        transform: scale(1.02);
      }
      .upload-label {
        cursor: pointer;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 12px;
      }
      .upload-icon {
        font-size: 48px;
        opacity: 0.6;
      }
      .upload-text {
        text-align: center;
      }
      .upload-text p {
        margin: 0 0 4px 0;
        font-size: 16px;
      }
      .upload-text strong {
        color: #1f2937;
        font-weight: 600;
      }
      .upload-text small {
        color: #9ca3af;
        font-size: 12px;
      }
      .findoc-files-list {
        margin-bottom: 20px;
      }
      .findoc-files-list h3 {
        font-size: 16px;
        font-weight: 600;
        margin: 0 0 12px 0;
        color: #1f2937;
      }
      #files-container {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      .file-item {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 12px;
        background: #f9fafb;
        border-radius: 6px;
        border: 1px solid #e5e7eb;
        animation: slideIn 0.3s ease;
      }
      .file-info {
        display: flex;
        align-items: center;
        gap: 8px;
        flex: 1;
        min-width: 0;
      }
      .file-icon {
        font-size: 20px;
        flex-shrink: 0;
      }
      .file-name {
        font-size: 14px;
        color: #1f2937;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .file-status {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-shrink: 0;
      }
      .progress-bar {
        width: 100px;
        height: 6px;
        background: #e5e7eb;
        border-radius: 3px;
        overflow: hidden;
      }
      .progress-fill {
        height: 100%;
        background: linear-gradient(90deg, #3b82f6, #2563eb);
        transition: width 0.3s ease;
        border-radius: 3px;
      }
      .status-icon {
        font-size: 18px;
      }
      .status-text {
        font-size: 13px;
        color: #6b7280;
        white-space: nowrap;
      }
      .findoc-actions {
        display: flex;
        gap: 12px;
        justify-content: flex-end;
      }
      .btn-primary, .btn-secondary {
        padding: 10px 20px;
        border-radius: 6px;
        font-size: 14px;
        font-weight: 500;
        cursor: pointer;
        border: none;
        transition: all 0.2s ease;
        font-family: inherit;
      }
      .btn-primary {
        background: #3b82f6;
        color: white;
      }
      .btn-primary:hover:not(:disabled) {
        background: #2563eb;
        transform: translateY(-1px);
        box-shadow: 0 4px 6px -1px rgba(59, 130, 246, 0.3);
      }
      .btn-primary:active:not(:disabled) {
        transform: translateY(0);
      }
      .btn-primary:disabled {
        background: #d1d5db;
        cursor: not-allowed;
        opacity: 0.6;
      }
      .btn-secondary {
        background: #f3f4f6;
        color: #374151;
        border: 1px solid #e5e7eb;
      }
      .btn-secondary:hover {
        background: #e5e7eb;
      }
      .btn-secondary:active {
        background: #d1d5db;
      }
      @keyframes slideIn {
        from {
          opacity: 0;
          transform: translateY(-10px);
        }
        to {
          opacity: 1;
          transform: translateY(0);
        }
      }
      @media (max-width: 640px) {
        .findoc-widget {
          padding: 16px;
        }
        .findoc-header h2 {
          font-size: 20px;
        }
        .findoc-upload-area {
          padding: 30px 15px;
        }
        .progress-bar {
          width: 60px;
        }
        .findoc-actions {
          flex-direction: column;
        }
        .btn-primary, .btn-secondary {
          width: 100%;
        }
      }
    `;
    }

    private attachEventListeners() {
        const fileInput = this.shadow.getElementById('file-input') as HTMLInputElement;
        const uploadArea = this.shadow.getElementById('upload-area');
        const uploadBtn = this.shadow.getElementById('upload-btn');
        const cancelBtn = this.shadow.getElementById('cancel-btn');

        fileInput?.addEventListener('change', (e) => {
            const target = e.target as HTMLInputElement;
            if (target.files) {
                this.handleFiles(Array.from(target.files));
            }
        });

        uploadArea?.addEventListener('dragover', (e) => {
            e.preventDefault();
            uploadArea.classList.add('dragover');
        });

        uploadArea?.addEventListener('dragleave', () => {
            uploadArea.classList.remove('dragover');
        });

        uploadArea?.addEventListener('drop', (e) => {
            e.preventDefault();
            uploadArea.classList.remove('dragover');

            if (e.dataTransfer?.files) {
                const pdfFiles = Array.from(e.dataTransfer.files).filter(
                    f => f.type === 'application/pdf'
                );
                this.handleFiles(pdfFiles);
            }
        });

        uploadBtn?.addEventListener('click', () => {
            this.processFiles();
        });

        cancelBtn?.addEventListener('click', () => {
            this.reset();
        });
    }

    private handleFiles(files: File[]) {
        files.forEach(file => {
            this.fileStatuses.set(file.name, {
                file,
                status: 'pending',
                progress: 0,
                message: 'Pronto para processar'
            });
        });

        this.renderFilesList();
        this.updateUploadButton();
    }

    private renderFilesList() {
        const filesList = this.shadow.getElementById('files-list');
        const filesContainer = this.shadow.getElementById('files-container');

        if (!filesList || !filesContainer) return;

        if (this.fileStatuses.size > 0) {
            filesList.style.display = 'block';

            filesContainer.innerHTML = '';
            this.fileStatuses.forEach((status, fileName) => {
                const fileItem = this.createFileItem(fileName, status);
                filesContainer.appendChild(fileItem);
            });
        } else {
            filesList.style.display = 'none';
        }
    }

    private createFileItem(fileName: string, status: FileStatus): HTMLElement {
        const div = document.createElement('div');
        div.className = 'file-item';
        div.id = `file-${this.sanitizeFileName(fileName)}`;

        const statusIcon = this.getStatusIcon(status.status);
        const showProgress = status.status === 'uploading' || status.status === 'processing';

        div.innerHTML = `
      <div class="file-info">
        <span class="file-icon">📄</span>
        <span class="file-name" title="${fileName}">${fileName}</span>
      </div>
      <div class="file-status">
        ${showProgress ? `
          <div class="progress-bar">
            <div class="progress-fill" style="width: ${status.progress}%"></div>
          </div>
        ` : `
          <span class="status-icon">${statusIcon}</span>
        `}
        <span class="status-text">${status.message}</span>
      </div>
    `;

        return div;
    }

    private getStatusIcon(status: FileStatus['status']): string {
        const icons = {
            pending: '⏳',
            uploading: '⬆️',
            processing: '⚙️',
            completed: '✅',
            error: '❌'
        };
        return icons[status] || '❓';
    }

    private sanitizeFileName(fileName: string): string {
        return fileName.replace(/[^a-z0-9]/gi, '_');
    }

    private updateUploadButton() {
        const uploadBtn = this.shadow.getElementById('upload-btn') as HTMLButtonElement;
        if (uploadBtn) {
            uploadBtn.disabled = this.fileStatuses.size === 0;
        }
    }

    private async processFiles() {
        if (!this.config || !this.apiClient) {
            console.error('Widget não configurado! Chame .configure() primeiro.');
            return;
        }

        const uploadBtn = this.shadow.getElementById('upload-btn') as HTMLButtonElement;
        uploadBtn.disabled = true;
        uploadBtn.textContent = 'Processando...';

        for (const [fileName, status] of this.fileStatuses) {
            if (status.status === 'pending') {
                await this.processFile(fileName, status);
            }
        }

        const allCompleted = Array.from(this.fileStatuses.values())
            .every(s => s.status === 'completed' || s.status === 'error');

        if (allCompleted) {
            uploadBtn.textContent = 'Concluído!';
            setTimeout(() => {
                uploadBtn.textContent = 'Processar arquivos';
                uploadBtn.disabled = false;
            }, 2000);
        }
    }

    private async processFile(fileName: string, status: FileStatus) {
        if (!this.apiClient || !this.config) return;

        try {
            this.updateFileStatus(fileName, 'uploading', 20, 'Enviando arquivo...');

            const analysisId = await this.apiClient.uploadPDF(
                status.file,
                this.config.clientId
            );

            status.analysisId = analysisId;

            this.updateFileStatus(fileName, 'processing', 50, 'Analisando com IA...');

            const result = await this.apiClient.pollForResult(analysisId);

            this.updateFileStatus(fileName, 'completed', 100, 'Análise concluída!');
            status.result = result;

            this.config.onSuccess?.(result);

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Erro desconhecido';
            this.updateFileStatus(fileName, 'error', 0, errorMessage);
            status.error = errorMessage;

            this.config.onError?.(errorMessage);
        }
    }

    private updateFileStatus(
        fileName: string,
        status: FileStatus['status'],
        progress: number,
        message: string
    ) {
        const fileStatus = this.fileStatuses.get(fileName);
        if (!fileStatus) return;

        fileStatus.status = status;
        fileStatus.progress = progress;
        fileStatus.message = message;

        this.fileStatuses.set(fileName, fileStatus);

        const fileItem = this.shadow.getElementById(`file-${this.sanitizeFileName(fileName)}`);
        if (fileItem) {
            const newItem = this.createFileItem(fileName, fileStatus);
            fileItem.replaceWith(newItem);
        }

        this.config?.onProgress?.(fileName, progress, message);
    }

    private reset() {
        this.fileStatuses.clear();
        this.renderFilesList();

        const fileInput = this.shadow.getElementById('file-input') as HTMLInputElement;
        if (fileInput) fileInput.value = '';

        const uploadBtn = this.shadow.getElementById('upload-btn') as HTMLButtonElement;
        uploadBtn.disabled = true;
        uploadBtn.textContent = 'Processar arquivos';
    }
}

// ✅ IMPORTANTE: Registrar o custom element IMEDIATAMENTE
customElements.define('findoc-upload-widget', FinDocUploadWidget);

// Exportar para uso programático (opcional)
export { FinDocUploadWidget };
export default FinDocUploadWidget;