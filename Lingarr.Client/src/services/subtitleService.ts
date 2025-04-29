import { AxiosError, AxiosResponse, AxiosStatic } from 'axios'
import { ISubtitleService } from '@/ts'

const service = (http: AxiosStatic, resource = '/api/subtitle'): ISubtitleService => ({
    collect<T>(path: string): Promise<T> {
        return new Promise((resolve, reject) => {
            http.post(
                `${resource}/all`,
                {
                    path: path
                },
                {
                    headers: {
                        'Cache-Control': 'no-cache, no-store, must-revalidate',
                        Pragma: 'no-cache',
                        Expires: '0'
                    }
                }
            )
                .then((response: AxiosResponse<T>) => {
                    resolve(response.data)
                })
                .catch((error: AxiosError) => {
                    reject(error.response)
                })
        })
    },
    
    downloadSubtitle(path: string): Promise<Blob> {
        return new Promise((resolve, reject) => {
            http.get(`${resource}/download`, {
                params: {
                    path: path
                },
                responseType: 'blob',
                headers: {
                    'Cache-Control': 'no-cache, no-store, must-revalidate',
                    Pragma: 'no-cache',
                    Expires: '0'
                }
            })
                .then((response: AxiosResponse<Blob>) => {
                    resolve(response.data)
                })
                .catch((error: AxiosError) => {
                    reject(error.response)
                })
        })
    },
    
    uploadSubtitle<T>(file: File, mediaPath: string, language: string): Promise<T> {
        return new Promise((resolve, reject) => {
            const formData = new FormData()
            formData.append('file', file)
            formData.append('mediaPath', mediaPath)
            formData.append('language', language)
            
            http.post(`${resource}/upload`, formData, {
                headers: {
                    'Content-Type': 'multipart/form-data',
                    'Cache-Control': 'no-cache'
                }
            })
                .then((response: AxiosResponse<T>) => {
                    resolve(response.data)
                })
                .catch((error: AxiosError) => {
                    reject(error.response)
                })
        })
    }
})

export const subtitleService = (axios: AxiosStatic): ISubtitleService => {
    return service(axios)
}
