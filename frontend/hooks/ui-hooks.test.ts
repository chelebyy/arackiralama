import { act, renderHook } from "@testing-library/react";
import type React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { reducer, toast, useToast } from "./use-toast";
import { formatBytes, useFileUpload } from "./use-file-upload";

describe("useToast", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("adds, updates, dismisses, and removes toasts through the reducer", () => {
    const added = reducer(
      { toasts: [] },
      {
        type: "ADD_TOAST",
        toast: { id: "toast-1", title: "Created", open: true },
      },
    );

    expect(added.toasts).toHaveLength(1);
    expect(added.toasts[0].title).toBe("Created");

    const updated = reducer(added, {
      type: "UPDATE_TOAST",
      toast: { id: "toast-1", title: "Updated" },
    });
    expect(updated.toasts[0].title).toBe("Updated");

    const dismissed = reducer(updated, {
      type: "DISMISS_TOAST",
      toastId: "toast-1",
    });
    expect(dismissed.toasts[0].open).toBe(false);

    const removed = reducer(dismissed, {
      type: "REMOVE_TOAST",
      toastId: "toast-1",
    });
    expect(removed.toasts).toEqual([]);
    expect(reducer(updated, { type: "REMOVE_TOAST", toastId: undefined }).toasts).toEqual([]);
  });

  it("publishes toast state to subscribers and supports imperative updates", () => {
    const { result, unmount } = renderHook(() => useToast());

    let created: ReturnType<typeof toast>;
    act(() => {
      created = toast({ title: "Queued", description: "Waiting" });
    });

    expect(result.current.toasts[0]).toMatchObject({
      id: created!.id,
      title: "Queued",
      open: true,
    });

    act(() => {
      created!.update({ id: created!.id, title: "Changed" });
    });
    expect(result.current.toasts[0].title).toBe("Changed");

    act(() => {
      result.current.toasts[0].onOpenChange?.(false);
    });
    expect(result.current.toasts[0].open).toBe(false);

    act(() => {
      vi.runOnlyPendingTimers();
    });
    expect(result.current.toasts).toEqual([]);

    unmount();
  });
});

describe("useFileUpload", () => {
  const createObjectURL = vi.fn((file: File) => `blob:${file.name}`);
  const revokeObjectURL = vi.fn();

  beforeEach(() => {
    createObjectURL.mockClear();
    revokeObjectURL.mockClear();
    vi.stubGlobal("URL", {
      createObjectURL,
      revokeObjectURL,
    });
    vi.spyOn(Date, "now").mockReturnValue(1779024000000);
    vi.spyOn(Math, "random").mockReturnValue(0.123456);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("formats byte sizes for display", () => {
    expect(formatBytes(0)).toBe("0 Bytes");
    expect(formatBytes(1024)).toBe("1KB");
    expect(formatBytes(1536, 1)).toBe("1.5KB");
    expect(formatBytes(1536, -1)).toBe("2KB");
  });

  it("adds, deduplicates, removes, and clears files with previews", () => {
    const onFilesChange = vi.fn();
    const onFilesAdded = vi.fn();
    const image = new File(["avatar"], "avatar.png", { type: "image/png" });
    const text = new File(["notes"], "notes.txt", { type: "text/plain" });

    const { result } = renderHook(() =>
      useFileUpload({
        multiple: true,
        maxFiles: 3,
        accept: "image/*,.txt",
        onFilesChange,
        onFilesAdded,
      }),
    );

    act(() => {
      result.current[1].addFiles([image, text]);
    });

    expect(result.current[0].files).toHaveLength(2);
    expect(result.current[0].files[0].preview).toBe("blob:avatar.png");
    expect(onFilesAdded).toHaveBeenCalledWith(expect.arrayContaining([
      expect.objectContaining({ file: image }),
      expect.objectContaining({ file: text }),
    ]));

    act(() => {
      result.current[1].addFiles([image]);
    });
    expect(result.current[0].files).toHaveLength(2);

    act(() => {
      result.current[1].removeFile(result.current[0].files[0].id);
    });
    expect(result.current[0].files).toHaveLength(1);
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:avatar.png");

    act(() => {
      result.current[1].clearFiles();
    });
    expect(result.current[0].files).toEqual([]);
  });

  it("reports validation errors for size, type, and max file limits", () => {
    const { result } = renderHook(() =>
      useFileUpload({
        multiple: true,
        maxFiles: 1,
        maxSize: 5,
        accept: ".png",
      }),
    );

    act(() => {
      result.current[1].addFiles([
        new File(["large-file"], "large.png", { type: "image/png" }),
        new File(["bad"], "bad.pdf", { type: "application/pdf" }),
      ]);
    });
    expect(result.current[0].errors).toEqual(["You can only upload a maximum of 1 files."]);

    const single = renderHook(() =>
      useFileUpload({ multiple: false, maxSize: 5, accept: ".png" }),
    );
    act(() => {
      single.result.current[1].addFiles([
        new File(["bad"], "bad.pdf", { type: "application/pdf" }),
      ]);
    });
    expect(single.result.current[0].errors[0]).toContain("not an accepted file type");

    act(() => {
      single.result.current[1].addFiles([
        new File(["large-file"], "large.png", { type: "image/png" }),
      ]);
    });
    expect(single.result.current[0].errors[0]).toContain("maximum size");

    act(() => {
      single.result.current[1].clearErrors();
    });
    expect(single.result.current[0].errors).toEqual([]);
  });

  it("handles drag, drop, file input props, and file dialog actions", () => {
    const droppedFile = new File(["drop"], "drop.png", { type: "image/png" });
    const { result } = renderHook(() => useFileUpload({ accept: "image/*" }));
    const input = document.createElement("input");
    const click = vi.spyOn(input, "click");
    const inputProps = result.current[1].getInputProps({ disabled: false });
    (inputProps.ref as React.MutableRefObject<HTMLInputElement>).current = input;

    const dragEvent = {
      preventDefault: vi.fn(),
      stopPropagation: vi.fn(),
      currentTarget: document.createElement("div"),
      relatedTarget: null,
    };

    act(() => {
      result.current[1].handleDragEnter(dragEvent as any);
    });
    expect(result.current[0].isDragging).toBe(true);

    act(() => {
      result.current[1].handleDragLeave(dragEvent as any);
    });
    expect(result.current[0].isDragging).toBe(false);

    act(() => {
      result.current[1].handleDragOver(dragEvent as any);
    });
    expect(dragEvent.preventDefault).toHaveBeenCalled();

    act(() => {
      result.current[1].handleDrop({
        ...dragEvent,
        dataTransfer: { files: [droppedFile] },
      } as any);
    });
    expect(result.current[0].files[0].file).toBe(droppedFile);

    act(() => {
      inputProps.onChange?.({
        target: { files: [new File(["change"], "change.png", { type: "image/png" })] },
      } as any);
    });
    expect(result.current[0].files[0].file.name).toBe("change.png");

    act(() => {
      result.current[1].openFileDialog();
    });
    expect(click).toHaveBeenCalled();

    input.disabled = true;
    act(() => {
      result.current[1].handleDrop({
        ...dragEvent,
        dataTransfer: { files: [droppedFile] },
      } as any);
    });
    expect(result.current[0].files[0].file.name).toBe("change.png");
  });
});
