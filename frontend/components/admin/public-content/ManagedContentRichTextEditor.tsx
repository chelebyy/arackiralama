"use client";

import { useEffect } from "react";
import { EditorContent, useEditor } from "@tiptap/react";
import Link from "@tiptap/extension-link";
import Placeholder from "@tiptap/extension-placeholder";
import Underline from "@tiptap/extension-underline";
import StarterKit from "@tiptap/starter-kit";
import { Bold, Italic, LinkIcon, List, ListOrdered, Redo, UnderlineIcon, Undo } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type ManagedContentRichTextEditorProps = {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
};

const allowedLinkProtocolPattern = /^(https?:|mailto:|tel:)/i;

function isAllowedManagedContentLink(url: string) {
  const normalizedUrl = url.trim();
  return normalizedUrl.length > 0 && !normalizedUrl.startsWith("//") && allowedLinkProtocolPattern.test(normalizedUrl);
}

export default function ManagedContentRichTextEditor({
  value,
  onChange,
  placeholder = "İçeriği yazın",
}: ManagedContentRichTextEditorProps) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({ codeBlock: false, code: false, horizontalRule: false }),
      Underline,
      Link.configure({
        openOnClick: false,
        autolink: false,
        defaultProtocol: "https",
        isAllowedUri: (url, ctx) => ctx.defaultValidate(url) && isAllowedManagedContentLink(url),
      }),
      Placeholder.configure({ placeholder }),
    ],
    content: value || "<p></p>",
    onUpdate: ({ editor: currentEditor }) => onChange(currentEditor.getHTML()),
  });

  useEffect(() => {
    if (!editor) {
      return;
    }

    const nextValue = value || "<p></p>";
    if (editor.getHTML() !== nextValue) {
      editor.commands.setContent(nextValue, { emitUpdate: false });
    }
  }, [editor, value]);

  if (!editor) {
    return null;
  }

  const addLink = () => {
    const currentHref = editor.getAttributes("link").href as string | undefined;
    const href = window.prompt("Link URL", currentHref ?? "");

    if (href === null) {
      return;
    }

    const normalizedHref = href.trim();
    if (!normalizedHref) {
      editor.chain().focus().extendMarkRange("link").unsetLink().run();
      return;
    }

    if (!isAllowedManagedContentLink(normalizedHref)) {
      toast.error("Sadece http(s), mailto veya tel linkleri kullanılabilir.");
      return;
    }

    editor.chain().focus().extendMarkRange("link").setLink({ href: normalizedHref }).run();
  };

  return (
    <div className="rounded-md border bg-background">
      <div className="flex flex-wrap gap-1 border-b p-2">
        <Button
          type="button"
          variant={editor.isActive("bold") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={() => editor.chain().focus().toggleBold().run()}
          aria-label="Kalın"
          aria-pressed={editor.isActive("bold")}
        >
          <Bold className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant={editor.isActive("italic") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={() => editor.chain().focus().toggleItalic().run()}
          aria-label="İtalik"
          aria-pressed={editor.isActive("italic")}
        >
          <Italic className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant={editor.isActive("underline") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={() => editor.chain().focus().toggleUnderline().run()}
          aria-label="Altı çizili"
          aria-pressed={editor.isActive("underline")}
        >
          <UnderlineIcon className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant={editor.isActive("bulletList") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={() => editor.chain().focus().toggleBulletList().run()}
          aria-label="Madde listesi"
          aria-pressed={editor.isActive("bulletList")}
        >
          <List className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant={editor.isActive("orderedList") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={() => editor.chain().focus().toggleOrderedList().run()}
          aria-label="Numaralı liste"
          aria-pressed={editor.isActive("orderedList")}
        >
          <ListOrdered className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant={editor.isActive("link") ? "secondary" : "ghost"}
          size="icon-sm"
          onClick={addLink}
          aria-label="Link ekle"
          aria-pressed={editor.isActive("link")}
        >
          <LinkIcon className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={() => editor.chain().focus().undo().run()}
          aria-label="Geri al"
          disabled={!editor.can().undo()}
        >
          <Undo className="h-4 w-4" />
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={() => editor.chain().focus().redo().run()}
          aria-label="Yinele"
          disabled={!editor.can().redo()}
        >
          <Redo className="h-4 w-4" />
        </Button>
      </div>
      <EditorContent
        editor={editor}
        className={cn(
          "min-h-48 cursor-text px-3 py-2 text-sm",
          "[&_.tiptap]:min-h-44 [&_.tiptap]:outline-none [&_.tiptap_p]:my-2",
          "[&_.tiptap_ul]:my-2 [&_.tiptap_ul]:list-disc [&_.tiptap_ul]:pl-5",
          "[&_.tiptap_ol]:my-2 [&_.tiptap_ol]:list-decimal [&_.tiptap_ol]:pl-5",
          "[&_.tiptap_a]:text-primary [&_.tiptap_a]:underline",
        )}
      />
    </div>
  );
}
