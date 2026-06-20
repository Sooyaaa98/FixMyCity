// src/app/citizen/submit-complaint/submit-complaint.component.ts

import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil, filter } from 'rxjs/operators';

import { ComplaintService } from '../../fmc-services/complaint.service';
import { AuthService } from '../../fmc-services/auth.service';
import { MlService } from '../../fmc-services/ml.service';
import { ToastService } from '../../fmc-services/toast.service';
import { SessionService } from '../../core/services/session.service';
import { IIssueCategory, ILocality } from '../../fmc-interfaces/complaint.interface';
import { ICategorySuggestion, IDuplicateResult } from '../../fmc-interfaces/ml.interface';
// Phase 8 (§2) — auto-save draft so accidental navigation doesn't lose work.
import { ComplaintDraftService } from '../../shared/utils/complaint-draft.service';

@Component({
  selector: 'app-submit-complaint',
  templateUrl: './submit-complaint.component.html',
  styleUrls: ['./submit-complaint.component.css']
})
export class SubmitComplaintComponent implements OnInit, OnDestroy {

  submitForm!: FormGroup;
  categories: IIssueCategory[] = [];
  localities: any[] = [];
  isLoading = false;
  successMessage = '';

  // AI hint state
  aiLoading = false;
  aiVisible = false;
  aiSuggestions: ICategorySuggestion[] = [];
  aiSuggestedDescription = '';
  duplicateResult: IDuplicateResult | null = null;
  duplicateChecked = false;    // true once duplicate check has run
  allowSubmitDespiteDuplicate = false;

  // Photo upload state
  photoFile: File | null = null;
  photoFilePath: string | null = null;        // server-side filename (basename)
  photoFileName: string | null = null;        // original client filename
  photoFileSizeKB: number | null = null;
  photoPreviewUrl: string | null = null;
  photoUploading = false;
  photoAnalysing = false;
  photoMessage = '';
  photoError = '';
  /** Phase 6 — true while a file is hovering over the dropzone. */
  isDragging = false;

  readonly criticalityOptions = ['Low', 'Medium', 'High', 'Critical'];

  // Phase 8 (§2) — true when a previous draft was found and the citizen
  // hasn't yet decided whether to restore or discard it.
  hasDraft = false;
  draftSavedAt: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private complaintService: ComplaintService,
    private authService: AuthService,
    private ml: MlService,
    private toast: ToastService,
    private session: SessionService,
    private router: Router,
    private drafts: ComplaintDraftService,
  ) {}

  ngOnInit(): void {
    this.submitForm = this.fb.group({
      title:       ['', [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
      categoryId:  [null, Validators.required],
      description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]],
      localityId:  [this.session.getLocalityId() || null, Validators.required],
      address:     ['', [Validators.required, Validators.maxLength(500)]],
      criticality: ['Low', Validators.required],
    });

    this.authService.getAllCategories().subscribe(cats => this.categories = cats);
    this.authService.getAllLocalities().subscribe(locs => this.localities = locs);

    // AI category suggestion: debounce on description field
    this.submitForm.get('description')!.valueChanges.pipe(
      debounceTime(900),
      distinctUntilChanged(),
      filter(val => val && val.length >= 20),
      takeUntil(this.destroy$)
    ).subscribe(() => this.suggestCategory());

    // Phase 8 (§2) — surface a "Restore draft?" prompt if one exists, and
    // auto-save anything the citizen types from now on.
    const draft = this.drafts.load();
    if (draft) {
      this.hasDraft = true;
      this.draftSavedAt = draft.savedAt;
    }
    this.submitForm.valueChanges.pipe(
      debounceTime(750),
      takeUntil(this.destroy$),
    ).subscribe(v => {
      // Only save if at least the title or description has been touched —
      // otherwise we'd save an empty record on every page-load.
      if ((v?.title?.trim() || v?.description?.trim())) {
        this.drafts.save({
          citizenUserId: this.session.getUserId() ?? undefined,
          categoryId:    v.categoryId  ?? null,
          title:         v.title,
          description:   v.description,
          localityId:    v.localityId  ?? null,
          address:       v.address,
          criticality:   v.criticality,
          latitude:      (this.submitForm as any).gpsLat ?? null,
          longitude:     (this.submitForm as any).gpsLon ?? null,
        });
      }
    });
  }

  /** Phase 8 (§2) — apply the saved draft to the form. */
  restoreDraft(): void {
    const d = this.drafts.load();
    if (!d) { this.hasDraft = false; return; }
    this.submitForm.patchValue({
      title:       d.title       ?? '',
      categoryId:  d.categoryId  ?? null,
      description: d.description ?? '',
      localityId:  d.localityId  ?? null,
      address:     d.address     ?? '',
      criticality: d.criticality ?? 'Low',
    });
    if (typeof d.latitude  === 'number') (this.submitForm as any).gpsLat = d.latitude;
    if (typeof d.longitude === 'number') (this.submitForm as any).gpsLon = d.longitude;
    this.hasDraft = false;
    this.toast.info('Draft restored.');
  }

  /** Phase 8 (§2) — explicitly discard the saved draft. */
  discardDraft(): void {
    this.drafts.clear();
    this.hasDraft = false;
    this.toast.info('Draft discarded.');
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── AI: category suggestion ───────────────────────────────────────────────

  suggestCategory(): void {
    const title = this.submitForm.get('title')!.value ?? '';
    const description = this.submitForm.get('description')!.value ?? '';
    if (!title && !description) return;

    this.aiLoading = true;
    this.aiVisible = true;

    this.ml.categorizeTextFull({ complaintId: 0, title, description }).subscribe(res => {
      this.aiSuggestions = res.suggestions ?? [];
      if (res.suggestedDescription) this.aiSuggestedDescription = res.suggestedDescription;
      this.aiLoading = false;
    });
  }

  onCategoryAccepted(categoryId: number): void {
    this.submitForm.patchValue({ categoryId });
    this.aiSuggestions = [];
    this.toast.info('Category updated from AI suggestion.');
  }

  /**
   * Replace the current description with the AI-drafted one. The AI draft is
   * derived from the citizen's existing text + top category + (if a photo was
   * uploaded) OCR text from the image, so it's never just boilerplate.
   */
  useAiDescription(): void {
    if (!this.aiSuggestedDescription) return;
    this.submitForm.patchValue({ description: this.aiSuggestedDescription });
    this.toast.info('Description replaced with AI suggestion.');
  }

  // ── Photo upload + AI image analysis ──────────────────────────────────────

  /**
   * Triggered by the file input. Uploads the photo, then asks the AI service
   * to analyse it. The response includes:
   *   - category suggestions  → prefilled into the AI hint panel
   *   - suggested description → offered behind "Use AI description"
   *   - EXIF GPS              → prefilled into latitude/longitude + address
   */
  async onPhotoSelected(evt: Event): Promise<void> {
    const input = evt.target as HTMLInputElement;
    const file = input?.files?.[0];
    if (!file) return;
    this.handleFile(file, input);
  }

  // ── Drag-and-drop handlers (Phase 6) ──────────────────────────────────
  onDragEnter(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    if (this.photoUploading || this.photoAnalysing) return;
    this.isDragging = true;
  }
  onDragOver(e: DragEvent): void {
    // dragover MUST be cancelled for a drop to fire
    e.preventDefault();
    e.stopPropagation();
    if (this.photoUploading || this.photoAnalysing) return;
    this.isDragging = true;
  }
  onDragLeave(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;
  }
  onDrop(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;
    if (this.photoUploading || this.photoAnalysing) return;
    const file = e.dataTransfer?.files?.[0];
    if (file) this.handleFile(file);
  }

  /** Common path for picker + drop. Validates, previews, uploads, analyses. */
  private handleFile(file: File, input?: HTMLInputElement | null): void {
    if (!/\.(jpe?g|png|webp)$/i.test(file.name)) {
      this.photoError = 'Only JPG, PNG, or WEBP images are allowed.';
      if (input) input.value = '';
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.photoError = 'Image must be 10 MB or smaller.';
      if (input) input.value = '';
      return;
    }

    this.photoError = '';
    this.photoMessage = '';
    this.photoFile = file;
    if (this.photoPreviewUrl) URL.revokeObjectURL(this.photoPreviewUrl);
    this.photoPreviewUrl = URL.createObjectURL(file);
    this.photoUploading = true;

    this.complaintService.uploadComplaintImage(file).subscribe({
      next: (res) => {
        this.photoUploading = false;
        if (!res.success || !res.filePath) {
          this.photoError = res.message || 'Upload failed.';
          return;
        }
        this.photoFilePath   = res.filePath;
        this.photoFileName   = res.fileName ?? file.name;
        this.photoFileSizeKB = res.fileSizeKB ?? Math.max(1, Math.round(file.size / 1024));
        this.photoMessage    = 'Photo uploaded. Running AI analysis…';
        this.analysePhoto(res.filePath);
      },
      error: () => {
        this.photoUploading = false;
        this.photoError = 'Could not reach the server to upload your photo.';
      }
    });
  }

  private analysePhoto(filePath: string): void {
    this.photoAnalysing = true;
    this.aiVisible = true;

    this.ml.analyzeImage({ complaintId: 0, filePath }).subscribe({
      next: (result) => {
        this.photoAnalysing = false;
        if (!result) {
          this.photoMessage = 'Photo uploaded — AI analysis unavailable. You can still submit.';
          return;
        }

        if (Array.isArray(result.suggestions) && result.suggestions.length) {
          this.aiSuggestions = result.suggestions;
          // Auto-pick the top suggestion only if the user hasn't chosen one yet
          if (!this.submitForm.get('categoryId')!.value) {
            this.submitForm.patchValue({ categoryId: result.suggestions[0].categoryId });
          }
        }

        if (result.suggestedDescription) {
          this.aiSuggestedDescription = result.suggestedDescription;
          // Only auto-fill the description field if it's currently empty
          if (!this.submitForm.get('description')!.value) {
            this.submitForm.patchValue({ description: result.suggestedDescription });
          }
        }

        if (typeof result.gpsLat === 'number' && typeof result.gpsLon === 'number') {
          this.photoMessage = `Photo uploaded. GPS detected: ${result.gpsLat.toFixed(5)}, ${result.gpsLon.toFixed(5)} — coordinates and address hint pre-filled.`;
          // Stash GPS on the form for inclusion in submit payload
          (this.submitForm as any).gpsLat = result.gpsLat;
          (this.submitForm as any).gpsLon = result.gpsLon;
          const addrCtrl = this.submitForm.get('address')!;
          if (!addrCtrl.value || addrCtrl.value.trim().length === 0) {
            addrCtrl.setValue(`Near [GPS ${result.gpsLat.toFixed(5)}, ${result.gpsLon.toFixed(5)}] — please refine`);
          }
        } else {
          this.photoMessage = 'Photo uploaded. AI suggestions applied.';
        }
      },
      error: () => {
        this.photoAnalysing = false;
        this.photoMessage = 'Photo uploaded — AI analysis failed but you can still submit.';
      }
    });
  }

  clearPhoto(): void {
    if (this.photoPreviewUrl) URL.revokeObjectURL(this.photoPreviewUrl);
    this.photoFile = null;
    this.photoFilePath = null;
    this.photoFileName = null;
    this.photoFileSizeKB = null;
    this.photoPreviewUrl = null;
    this.photoMessage = '';
    this.photoError = '';
    (this.submitForm as any).gpsLat = undefined;
    (this.submitForm as any).gpsLon = undefined;
  }

  // ── Browser geolocation (Phase 4 — OI-17) ────────────────────────────────
  // EXIF-derived GPS works only when the citizen uploads a photo. This is a
  // direct, photo-less path: tap "Use my current location" and the browser's
  // geolocation API populates lat / lon plus a "Near […]" address hint, exactly
  // mirroring what the photo path does. Falls through silently when:
  //   - the browser doesn't support geolocation
  //   - the user denies the permission prompt
  //   - the lookup times out
  geoLoading = false;

  useMyLocation(): void {
    if (!('geolocation' in navigator)) {
      this.toast.error('Your browser does not support geolocation.');
      return;
    }
    this.geoLoading = true;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.geoLoading = false;
        const lat = pos.coords.latitude;
        const lon = pos.coords.longitude;
        (this.submitForm as any).gpsLat = lat;
        (this.submitForm as any).gpsLon = lon;

        const addrCtrl = this.submitForm.get('address')!;
        if (!addrCtrl.value || addrCtrl.value.trim().length === 0) {
          addrCtrl.setValue(`Near [GPS ${lat.toFixed(5)}, ${lon.toFixed(5)}] — please refine`);
        }
        this.toast.success(`Location captured: ${lat.toFixed(5)}, ${lon.toFixed(5)}.`);
      },
      (err) => {
        this.geoLoading = false;
        const msg = err.code === err.PERMISSION_DENIED
          ? 'Location permission denied. You can still type the address manually.'
          : err.code === err.TIMEOUT
            ? 'Location lookup timed out. Please try again or type the address.'
            : 'Could not read your location. Please type the address manually.';
        this.toast.info(msg);
      },
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 300_000 }
    );
  }

  get hasGps(): boolean {
    return typeof (this.submitForm as any)?.gpsLat === 'number';
  }

  // ── AI: duplicate check (called pre-submit) ───────────────────────────────

  private checkDuplicates(): Promise<boolean> {
    const { title, description, categoryId, localityId } = this.submitForm.value;
    if (!categoryId || !localityId) return Promise.resolve(true);

    return new Promise(resolve => {
      this.ml.checkDuplicates({
        complaintId: 0,
        title,
        description,
        categoryId: Number(categoryId),
        localityId: Number(localityId),
        excludeId: 0
      }).subscribe(res => {
        this.duplicateResult = res.result;
        this.duplicateChecked = true;
        this.aiVisible = true;
        resolve(res.result.count === 0 || this.allowSubmitDespiteDuplicate);
      });
    });
  }

  onSubmitAnyway(): void {
    this.allowSubmitDespiteDuplicate = true;
    this.duplicateResult = null;
    this.aiVisible = false;
    this.doSubmit();
  }

  // ── Submit flow ───────────────────────────────────────────────────────────

  async onSubmit(): Promise<void> {
    if (this.submitForm.invalid) {
      this.submitForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;

    // Run duplicate check unless user already acknowledged
    if (!this.allowSubmitDespiteDuplicate) {
      const canProceed = await this.checkDuplicates();
      if (!canProceed) {
        this.isLoading = false;
        return;  // duplicate panel now visible — user must respond
      }
    }

    this.doSubmit();
  }

  private doSubmit(): void {
    this.isLoading = true;
    const v = this.submitForm.value;
    const gpsLat = (this.submitForm as any).gpsLat;
    const gpsLon = (this.submitForm as any).gpsLon;
    const payload: any = {
      citizenUserId: this.session.getUserId(),
      categoryId:    Number(v.categoryId),
      title:         v.title,
      description:   v.description,
      localityId:    Number(v.localityId),
      address:       v.address,
      criticality:   v.criticality,
    };
    if (typeof gpsLat === 'number') payload.latitude  = gpsLat;
    if (typeof gpsLon === 'number') payload.longitude = gpsLon;
    if (this.photoFilePath) {
      payload.filePath   = this.photoFilePath;
      payload.fileName   = this.photoFileName;
      payload.fileSizeKB = this.photoFileSizeKB;
    }

    this.complaintService.submitComplaint(payload).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          // Phase 8 (§2) — successful submission wipes the local draft.
          this.drafts.clear();
          this.toast.success(`Complaint #${res.complaintId} submitted successfully!`);
          setTimeout(() => this.router.navigate(['/citizen/complaints']), 1500);
        } else {
          this.toast.error(res.message ?? 'Submission failed. Please try again.');
        }
      },
      error: () => { this.isLoading = false; }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  fieldError(name: string): string {
    const ctrl = this.submitForm.get(name);
    if (!ctrl?.invalid || !ctrl.touched) return '';
    if (ctrl.errors?.['required'])   return `${this.fieldLabel(name)} is required.`;
    if (ctrl.errors?.['minlength'])  return `Too short (min ${ctrl.errors['minlength'].requiredLength} chars).`;
    if (ctrl.errors?.['maxlength'])  return `Too long (max ${ctrl.errors['maxlength'].requiredLength} chars).`;
    return 'Invalid value.';
  }

  private fieldLabel(name: string): string {
    const labels: Record<string,string> = {
      title: 'Title', categoryId: 'Category', description: 'Description',
      localityId: 'Locality', address: 'Address', criticality: 'Criticality'
    };
    return labels[name] ?? name;
  }

  isInvalid(name: string): boolean {
    const ctrl = this.submitForm.get(name);
    return !!(ctrl?.invalid && ctrl.touched);
  }
}
