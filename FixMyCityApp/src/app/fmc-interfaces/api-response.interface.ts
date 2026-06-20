// src/app/fmc-interfaces/api-response.interface.ts

export interface IApiResponse {
  success: boolean;
  message?: string;
  error?: string;
}