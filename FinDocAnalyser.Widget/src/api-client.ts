export interface AnalysisResponse {
    analysisId: string;
}

export interface AnalysisResult {
    total: {
        totalInvestedAmount: number;
        currency: string;
    };
    classification: {
        totalInvested: number;
        currency: string;
        classes: Array<{
            assetClassName: string;
            invested: number;
            percentage: number;
            confidence: number;
            confidenceReason: string;
        }>;
    };
    stocks: {
        totalInvested: number;
        currency: string;
        stocks: Array<{
            ticker: string;
            quantity: number;
            averagePrice: number;
            totalInvested: number;
            currentValue: number;
            return: number | null;
            returnPercentage: number | null;
            confidence: number;
            confidenceReason: string;
        }>;
    };
    fixedIncome: {
        totalInvested: number;
        currency: string;
        assets: Array<{
            assetType: string;
            issuer: string;
            invested: number;
            currentValue: number;
            yieldRate: string;
            maturityDate: string | null;
            applicationDate: string | null;
            confidence: number;
            confidenceReason: string;
        }>;
    };
}

export class ApiClient {
    constructor(private apiUrl: string) { }

    async uploadPDF(file: File, clientId?: string): Promise<string> {
        const formData = new FormData();
        formData.append('file', file);

        if (clientId) {
            formData.append('clientId', clientId);
        }

        const response = await fetch(`${this.apiUrl}/api/analysis`, {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Erro ao enviar PDF: ${error}`);
        }

        const data: AnalysisResponse = await response.json();
        return data.analysisId;
    }

    async getAnalysisResult(analysisId: string): Promise<AnalysisResult> {
        const response = await fetch(
            `${this.apiUrl}/api/analysis/${analysisId}/complete`
        );

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Erro ao buscar resultado: ${error}`);
        }

        return await response.json();
    }

    async pollForResult(
        analysisId: string,
        maxAttempts: number = 30,
        intervalMs: number = 2000
    ): Promise<AnalysisResult> {
        for (let i = 0; i < maxAttempts; i++) {
            try {
                const result = await this.getAnalysisResult(analysisId);
                return result;
            } catch (error) {
                if (i === maxAttempts - 1) {
                    throw error;
                }
                await new Promise(resolve => setTimeout(resolve, intervalMs));
            }
        }
        throw new Error('Timeout aguardando resultado da análise');
    }
}