import api from "../lib/api";

export type UploadResponse = {
  url: string;
  fileName?: string;
  sizeBytes?: number;
};

export async function uploadImage(file: File): Promise<UploadResponse> {
  const form = new FormData();
  form.append("file", file);

  const { data } = await api.post<UploadResponse>("/api/uploads/image", form, {
    headers: { "Content-Type": "multipart/form-data" },
  });

  return data;
}
